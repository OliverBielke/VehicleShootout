# PacMan Shooter (assignment 4)

This assigment is an extension of the Capture-the-Flag (CTF) assignment, so the readme of that assignment is kept below for easy reference.

The key changes compared to CTF are as follows 
- All agents have a health 0-100, displayed in a health bar
- All agents have a gun
- Gun damage decreases linearly with distance
- Gun cannot be aimed, but automatically fires at the closest enemy with a free line of sight. Thus a 1 vs 1 fight always ends in a draw. The rationale for this is to focus effort on multi-agent coordination, not clever aiming. Gunfire is shown as a red line between corresponding agents.
- Health regenerates gradually when not shot during a few seconds
- The rest of the game is the same. You still win by collecting food. You can still eat (melee) opponents in your own turf. Etc. 

In the situation depicted below, both blue agents fire on the bottom red agent (closest), while both red agents fire on the right blue agent (closest). If all agents started from 100 health, the bottom red would die first, as damage is larger at short range. If the left blue agent approached the right red agent, he would draw fire, and the  blue would defeat the red in a 2-0 victory. If all agents remain stationary, the right blue agent would probably die before the remaining red, giving a 2-1 victory.

![alt text](PacManShooter1.png)

# PacMan Capture-The-Flag (assignment 3)

Inspired by UC Berkeley's Python environment: https://inst.eecs.berkeley.edu/~cs188/fa24/projects/

The Capture-the-flag game is played by two opposing teams, the red and the blue (see images below). In order for your team to be able to compete against other teams, without sharing source code and thereby exposing your brilliant code too early, we utilize the unity features of creating executables.

You will build your AI code into the [PacManAI](Assets/Scripts/PacManAI.cs) class, as in the previous assignments.
However, to manage the competitions we have provided a networked version of the game as well, using a custom synchronized HTTP server, on top of Protobuf messages.

We strongly suggest you test out your solutions in both **Local** and **Client/Server** mode **_AS EARLY AS POSSIBLE_**.
Otherwise you might run into unexpected issues during competitions.

It is possible to run the environment in one three ways:
- Normal Unity scene without networking (Local)
- Networked Client / Server 
- Replay mode 

![alt text](ModeSelection.png)

## Local

The local mode can be a convenient mode for testing changes quickly.

In the local mode, everything is run on one instance of the environment - as in previous assignments.
This is done by starting the unity editor as usual, picking the PacManNormal scene, and pressing play. To test things out you can control both sides using wasd and arrow keys.

Doing this you are running the environment with "Mode: Local" in PacManManagerModeSelector or by disabling the mode selector entirely.

Or alternatively from the command line, with the following command (assuming an executable named "PacManCTF"):
```
./PacManCTF -mode local
```
If you have a Mac, check instructions for running an executable from the terminal at the bottom of this page.

## Client / Server

In this mode, 3 instances of the application are necessary to run the game. Therefore, a built artifact is required to launch it in this manner. See below.

Once you have the executable, you can either:
- Run it 3 times to start 1 server and 2 clients. The server should be started first.
- Alternatively using this command (assuming an executable named "PacManCTF"):
```
 ./PacManCTF -mode server & ./PacManCTF -mode client & ./PacManCTF -mode client
```

The game is **server authoritative.** All data that is not supposed to be seen by the Client does not exist on the Client. Actions constructed on the client are sent to the server for execution and played back on the Client.

The game is **fully synchronous**. The server waits for both clients to send in their actions before stepping the environment. Thus if your executable never sends an action the game will not progress. This will not be a way to escape losing, so avoid it. Details on available computation time can be found on Canvas.

It is suggested you test your algorithms out in both modes.
Note that the **final competitions will be in Client/Server mode**, to enable you to share built executables and run games against other teams without having to manage their code.


## Replay

### Saving a replay
![alt text](Recorder.png)
To store a replay, enable the GameRecorder component and set a file name or enable from CLI (see below).

### Playing a replay

![alt text](ReplayFileSelect.png)
![alt text](ReplayMode.png)

To play back a replay, in the editor, **Change the game mode to Replay** and **select a file under the ReplayManager**. Then start the game in the editor. 



## Other command line options

It is possible to manually change the port of an executable 

```
 ./PacManCTF -port <number> -mode server
```

Or the map to be loaded during startup

```
 ./PacManCTF -map <name> 
```

Valid map names can be found in the map manager, as in previous assignments. A valid example would be
```
 ./PacManCTF -map pacmanA
``` 

Or enable recorder output for a server instance and choose the recording file base name

```
 ./PacManCTF -mode server -recording <name>
```
The `-recording` option is only applied on the server. When present, it forces the server-side `GameRecorder` to be active and uses `<name>` as the file base name.

# Building an executable

![alt text](build1.png)

It is best to build a single scene into the executable at once, unless you know what you are doing and how to select scenes.

![alt text](BuildNew1.png)

You can change the Target Platform in order to build versions that can be used by people on other plaforms.
In order for that to be available you have to add the corresponding modules to the version of the engine you are using (see below).

![alt text](BuildNew2.png)

NB! When building for different platforms, do not open or launch any of the files!
Some filesystems will cause the files to become corrupted and not launchable on the target platform.

If for some reason some of the platforms are grayed out, you can add support for those as follows:

![alt text](BuildNew3.png)

Your editor version might differ.

![alt text](BuildNew4.png)

Do not forget to restart your editor after installing new support modules.

NB! On some platforms you might need to delete your old build artifacts if you wish to build an artifact into the same folder as a previous one to see any changes.

Once the executable is built, you can run it directly  as any other application using the commands in the sections above.

# If you have a Mac
To run a Unity executable from the terminal on a Mac, you have to write a bit more. In macOS, an app is actually a folder, so assuming myApp is the name of the executable, instead of writing

```
 ./myApp 
```
you write
```
 ./myApp.app/Contents/MacOS/PacManCTF 
```
and add flags and arguments after as described above. Note that you can access the folders inside the executable using the `ls` command from the terminal as usual. On a Mac, the longer example above, to start both clients and server, would be:
```
./myApp.app/Contents/MacOS/PacManCTF -mode server & ./myApp.app/Contents/MacOS/PacManCTF -mode client & ./myApp.app/Contents/MacOS/PacManCTF -mode client
```
### Executable permissions for external executables

If you have an executable from a different machine your terminal will probably complain about permissions, in such case use the following commands in the terminal to be able to run the app.

Assuming a build called "Mac.app" :

```
sudo xattr -dr com.apple.quarantine Mac.app
sudo chmod +x ./Mac.app/Contents/MacOS/PacManCTF
```
