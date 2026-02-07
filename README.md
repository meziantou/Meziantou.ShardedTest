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

## How it works

The tool runs a subset of tests from a test project based on the provided parameters. It performs the following steps:

- Lists all available tests using `dotnet test --list-tests`
- Sorts tests deterministically using ordinal string comparison
- Selects a shard based on `--job-number` and `--total-jobs`
- Runs the selected tests using `dotnet test` with the appropriate filters
- Forwards all parameters except `--job-number` and `--total-jobs` to `dotnet test`
