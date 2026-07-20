using BenchmarkDotNet.Running;
using Mezon.Net.Client.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(ProtoListViewBenchmarks).Assembly).Run(args);
