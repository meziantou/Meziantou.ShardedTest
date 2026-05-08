namespace Meziantou.ShardedTest;

internal static class Program
{
	private const string UsageText = "Usage: Meziantou.ShardedTest --shard-index <n> --total-shards <n> [dotnet test arguments]";

	public static async Task<int> Main(string[] args)
	{
		if (!ArgumentParser.TryParse(args, out var parsed, out var error))
		{
			Console.Error.WriteLine(error);
			Console.Error.WriteLine(UsageText);
			return 1;
		}

		IReadOnlyList<string> tests;
		var listTestsRequested = ArgumentUtilities.HasListTestsArg(parsed.ForwardArgs);
		Console.WriteLine("Listing all tests...");

		try
		{
			tests = await DotnetTestService.ListTestsAsync(parsed.ForwardArgs, CancellationToken.None);
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.Message);
			return 1;
		}

		var selectedTests = TestSelector.SelectTests(tests, parsed.ShardIndex, parsed.TotalShards);
		Console.WriteLine($"Found {tests.Count} tests, running {selectedTests.Count} over {tests.Count} (shard {parsed.ShardIndex}/{parsed.TotalShards})");
		if (listTestsRequested)
		{
			WriteListTests(selectedTests);
			return 0;
		}
		if (selectedTests.Count == 0)
		{
			Console.WriteLine("No tests selected for this job.");
			return 0;
		}

		return await DotnetTestService.RunTestsAsync(parsed.ForwardArgs, tests, selectedTests, CancellationToken.None);
	}

	private static void WriteListTests(IReadOnlyList<string> tests)
	{
		Console.WriteLine("The following Tests are available:");
		foreach (var test in tests)
		{
			Console.WriteLine("    " + test);
		}

		Console.WriteLine($"Total tests: {tests.Count}");
	}
}
