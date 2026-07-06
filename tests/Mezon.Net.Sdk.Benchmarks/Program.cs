using BenchmarkDotNet.Running;
using Mezon.Net.Sdk.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(EntityCacheBenchmarks).Assembly).Run(args);
