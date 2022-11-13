using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace DynamicBenchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net70)]
[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
//[RPlotExporter]
public class DynamicPerformance
{
    object data;
    MemoryStream jsonStream;
    MemoryStream mPackStream;
    IMapper mapper;

    //[Params(10, 100, 1000)]
    public int Lines = 100;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public DynamicPerformance() => Setup();
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    [GlobalSetup]
#pragma warning disable xUnit1013 // Public method should be marked as test
    public void Setup()
#pragma warning restore xUnit1013 // Public method should be marked as test
    {
        var lines = new List<dynamic>();

        for (int i = 0; i < Lines; i++)
        {
            lines.Add(new
            {
                Start = new { X = 0, Y = 0 },
                End = new { X = 100, Y = 100 }
            });
        }

        dynamic data = new
        {
            Lines = lines.ToArray()
        };

        jsonStream = new MemoryStream();
        JsonSerializer.Serialize(jsonStream, (object)data);
        jsonStream.Position = 0;

        mPackStream = new MemoryStream();
        MessagePackSerializer.Serialize(typeof(Buffer), mPackStream, JsonSerializer.Deserialize<Buffer>(jsonStream));
        
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap(((object)data).GetType(), typeof(Buffer));
            cfg.CreateMap(((object)data.Lines[0]).GetType(), typeof(Line));
            cfg.CreateMap(((object)data.Lines[0].Start).GetType(), typeof(Point));
        });

        mapper = configuration.CreateMapper();
        this.data = data;
    }

    [Fact]
    [Benchmark]
    public void Dynamic()
    {
        var buffer = Dynamically.Create<Buffer>(data);
        Assert.All(buffer.Lines, line => Assert.Equal(100, line.End.Y));
    }

    [Fact]
    [Benchmark]
    public void AutoMapper()
    {
        var buffer = mapper.Map<Buffer>(data);
        Assert.All(buffer.Lines, line => Assert.Equal(100, line.End.Y));
    }

    [Fact]
    [Benchmark]
    public void MessagePack()
    {
        mPackStream.Position = 0;
        var buffer = MessagePackSerializer.Deserialize<Buffer>(mPackStream);
        Assert.All(buffer!.Lines, line => Assert.Equal(100, line.End.Y));
    }

    [Fact]
    [Benchmark]
    public void Json()
    {
        jsonStream.Position = 0;
        var buffer = JsonSerializer.Deserialize<Buffer>(jsonStream);
        Assert.All(buffer!.Lines, line => Assert.Equal(100, line.End.Y));
    }
}

[MessagePackObject]
public partial record Point([property: Key(0)] int X, [property: Key(1)] int Y);

[MessagePackObject]
public partial record Line([property: Key(0)] Point Start, [property: Key(1)] Point End);

[MessagePackObject]
public partial record Buffer([property: Key(0)] List<Line> Lines);

static partial class Dynamically
{
    public static T Create<T>(dynamic data)
    {
        if (data is IDynamicMetaObjectProvider)
        {
            global::System.Console.WriteLine("Is provider!");
        }

        return typeof(T) switch
        {
            Type t when t == typeof(Buffer) => (T)Buffer.Create(data),
            Type t when t == typeof(Line) => (T)Line.Create(data),
            Type t when t == typeof(Point) => (T)Point.Create(data),
            _ => throw new NotSupportedException(),
        };
    }
}
