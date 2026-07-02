# BSTester
A tester program for testing the "Trampoline" exception issue when running modded Beat Saber under Proton.

## HOW TO RUN
1. Clone the repository or download the source code
2. Modify the constants in `BSTester/Program.cs` and `BSBisecter/Program.cs` to match your environment
3. Build the solution
4. Run the `BSBisecter` by using `./BSBisecter` **in the build output directory** to start testing

## How does it work?
### BSTester
This is the test runner that will launch Beat Saber and monitor the logs for the "Trampoline" exception. 
It will wait for the exception to appear or a timeout and then kill the game process.

### BSBisecter
This is the main program that will go through the commits of MonoMod and run `BSTester` after compiling and copying MonoMod.
It will run the test for each commit for at most 25 times and if the "Trampoline" exception is detected, it will checkout the previous commit and start the test again.
It will stop if the exception is not detected after 25 runs.

This is not actually bisecting because to work around changing API and ABI across commits, patches are applied before compiling MonoMod. 
See `patches/` for the patches and the `PatchIndex` dictionary in `BSBisecter/Program.cs` for the mapping of commits to patches.