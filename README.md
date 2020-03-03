

## Developer Environment

Setup: Windows 10, Visual Studio 8 SP1 + SIMPL# SDK (downloaded from Crestron website)

## Creating a new SIMPL# Project

1. Create a new SIMPL# Project

    - Open VS2008
    - File->New->Project->Crestron->SIMPL# Pro

## Uploading a Simpl# Program to a Crestron Processor

1. Toolbox->Program->Program##->Erase

2. Copy <program>.cpz from VS2008 build directory to Crestron Processor (ftp://<Crestron IP>/Program##)

3. Open Dev Console and load the program (replace ## with the program number)
```
    PRO3>progload -P:##
    Looking for *.lpz/*.cpz in the current program directory for App 3.
    Unzipping new program now for App 3................
    Registering Simpl Sharp PRO program with entry point RoomMonitor 
    Program Start successfully sent for App 3
    

    PRO3>**Restarting Program:3**
    ..............
    Program Boot Directory:03: \SIMPL\app03 
    Loading Application:03...
    Application Loaded :03
```

### Reloading a Program from Dev Console
```
PRO3>progreset -P:##
** Restarting Program ** 
 
PRO3> Stopping Program:3 
.
 Waiting for un-registration to complete:03............
Completing program stop:03 
**Program Stopped:3**
**Restarting Program:3**
..............
Program Boot Directory:03: \SIMPL\app03 
Loading Application:03.....
 Application Loaded :03
```


## References

1. [SimplSharp Libraries](http://www.nivloc.com/downloads/crestron/SSharp/include4.dat%20=%202.09.036%20-%20Plugin%202.x/DLLs/)
2. MQTT Testing: [HiveMQ](http://www.hivemq.com/demos/websocket-client/)

## Notes

### Sending Signals to your program

Simpl Windows Programs can communicate with a program through Ethernet Intersystems Communications devices. Both the Simpl Windows program and the Simpl# program need to connect to IP: 127.0.0.2 and the same IPID.