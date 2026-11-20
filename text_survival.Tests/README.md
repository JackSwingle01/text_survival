# Running tests

Run the functional suite (also used by CI):

```sh
dotnet test text_survival.sln
```

The test project's default runsettings exclude tests marked `Category=Performance`.
Run those timing and size guards explicitly on an otherwise idle machine:

```sh
dotnet test text_survival.Tests/text_survival.Tests.csproj -c Release --settings text_survival.Tests/performance.runsettings
```

An explicit `--filter` is combined with the runsettings filter. To select
functional tests by name, use:

```sh
dotnet test text_survival.Tests/text_survival.Tests.csproj --filter 'FullyQualifiedName~SerializationTests'
```

Use `TestHelpers.SmallGame.Create()` for tests that need a game graph but do not
exercise world generation or settling. Each call creates an independent three-tile
map with a player, campfire, storage, and forage. Full-game save/load and world
generation tests still use `GameContext.CreateNewGame()`.

The xUnit runner caps parallel test collections at four threads. Repeated local
Release measurements after the fixture cleanup reduced the full-suite median from
34.2 to 32.0 seconds with this cap. Native UI tests retain their existing serial
collection. Re-measure before changing the cap; results depend on the host.

For local UI or survival scenarios that need starting clothing and fire-making
supplies, use `SmallGame.CreateWithStartingEquipment()`. Add any other required
features, discoveries, or actors explicitly so the test does not depend on random
world generation.
