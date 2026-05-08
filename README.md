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

Split a test project into shards using `--shard-index` and `--total-shards`. All other arguments are passed to `dotnet test`.

Example:

```
sharded-test --shard-index 1 --total-shards 4 tests/MyTests.csproj --configuration Release
```

Use `--verbose` to print each executed `dotnet` command line.

## GitHub Actions example (3 jobs)

Use a matrix to split the test project into 3 shards. Each job runs a distinct shard while using the same total shard count.

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
				shard-index: [1, 2, 3]

		steps:
			- uses: actions/checkout@v4
			- name: Install sharded-test
				run: dotnet tool install --global Meziantou.ShardedTest

			- name: Run tests (shard ${{ matrix.shard-index }}/3)
				run: sharded-test --shard-index ${{ matrix.shard-index }} --total-shards 3 tests/MyTests.csproj
```

## GitLab CI example

GitLab CI sets `CI_NODE_INDEX` and `CI_NODE_TOTAL` automatically when using the [`parallel:`](https://docs.gitlab.com/ee/ci/yaml/#parallel) keyword. The tool reads these variables as fallback values, so no extra arguments are needed:

```yaml
test:
  parallel: 4
  script:
    - dotnet tool install --global Meziantou.ShardedTest
    - sharded-test tests/MyTests.csproj
```

## How it works

The tool runs a subset of tests from a test project based on the provided parameters. It performs the following steps:

- Lists all available tests using `dotnet test --list-tests`
- Sorts tests deterministically using ordinal string comparison
- Selects a shard based on `--shard-index` and `--total-shards`
- Runs the selected tests using `dotnet test` with the appropriate filters
- Forwards all parameters except `--shard-index` and `--total-shards` to `dotnet test`

Note that command line length can be a limiting factor when running a large number of tests. The tool automatically splits test filters into multiple `dotnet test` invocations if necessary.
