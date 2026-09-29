# Contributing to WireDrop

Fixes and features are welcome. WireDrop is free to use but not open source: you may
change the code only to send those changes here, and nothing ships except through this
repository. See [LICENSE](LICENSE).

## How

1. Fork this repository and make your change on a branch.
2. Build and test it:
   ```bash
   dotnet test tests/WireDrop.Tests
   dotnet build src/WireDrop -c Release
   ```
   Then copy `src/WireDrop/bin/Release/WireDrop.gha` into Grasshopper's Libraries folder
   and try it in Rhino 8.
3. Open a pull request saying what it changes and why. For anything large, open an issue
   first so we can agree on the approach before you put the work in.

Match the code around your change — its naming, its comment style, and how much it says.
Anything that can throw inside Grasshopper's event loop goes through `Log.Guard`.

Whether a change is merged is up to the maintainer.

## Contributor agreement

By opening a pull request, you agree that:

1. The contribution is your own work, or you have the right to submit it.
2. You grant Yolanda Xing a perpetual, worldwide, irrevocable, royalty-free license to
   use, modify, and distribute your contribution as part of WireDrop or any successor,
   under any license the maintainer chooses, including licenses other than the current
   one.
3. You keep the copyright in your contribution.
4. You may be credited by name in the release notes. Say so in the pull request if you
   would rather not be.
