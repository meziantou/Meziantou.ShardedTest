## How to install

Prerequisite: .NET SDK.

Install from NuGet as a .NET tool:

```
dotnet tool install --global Meziantou.ShardedTest
```

Update to the latest version:

```
dotnet tool update --global Meziantou.ShardedTest
```

## How to use

Split a test project into shards using `--job-number` and `--total-jobs`. All other arguments are passed to `dotnet test`.

Example:

```
sharded-test --job-number 1 --total-jobs 4 tests/MyTests.csproj --configuration Release
```

## GitHub Actions example (3 jobs)

Use a matrix to split the test project into 3 shards. Each job runs a distinct shard while using the same total job count.

```yaml
name: CI

on:
	push:
	pull_request:

jobs:
	test:
		runs-on: ubuntu-latest
		strategy:
			fail-fast: false
			matrix:
				job-number: [1, 2, 3]

		steps:
			- uses: actions/checkout@v4
			- name: Install sharded-test
				run: dotnet tool install --global Meziantou.ShardedTest

			- name: Run tests (shard ${{ matrix.job-number }}/3)
				run: sharded-test --job-number ${{ matrix.job-number }} --total-jobs 3 tests/MyTests.csproj
```

## How it works

The tool runs a subset of tests from a test project based on the provided parameters. It performs the following steps:

- Lists all available tests using `dotnet test --list-tests`
- Sorts tests deterministically using ordinal string comparison
- Selects a shard based on `--job-number` and `--total-jobs`
- Runs the selected tests using `dotnet test` with the appropriate filters
- Forwards all parameters except `--job-number` and `--total-jobs` to `dotnet test`

Note that command line length can be a limiting factor when running a large number of tests. The tool automatically splits test filters into multiple `dotnet test` invocations if necessary.