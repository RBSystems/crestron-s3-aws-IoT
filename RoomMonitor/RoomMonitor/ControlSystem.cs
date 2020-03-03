using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.GeneralIO;
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using PepperDash.Core;
using PepperDash.Essentials.Core;
using Crestron.SimplSharpPro.Gateways;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharpPro.EthernetCommunication;
using System.Collections.Generic;
// Data serialization
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace RoomMonitor
{
    public class ControlSystem : CrestronControlSystem
    {
        // Object used to store property/room information such as occupancy
        Property property;
        // Object used to temporarily store room information as it's received before permanatly storing in property object
        Room _room;

        AWSIoT iot;

        /// <summary>
        /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// 
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// 
        /// You cannot send / receive data in the constructor
        /// </summary>
        public ControlSystem()
            : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);

                // Connect to EIC to receive signals from SIMPL Program
                EthernetIntersystemCommunications eic = new EthernetIntersystemCommunications(0xfe, "127.0.0.2", this);
                // Add the signal handler
                eic.SigChange += new SigEventHandler(EICSignalHandler);
                eic.Register();

            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        private enum eSignalDigitalID
        {
            Unit_Is_Online = 1,
            TSTAT_Battery_Low_FB = 4,
            OCC_Battery_Low_FB = 5,
            Occupancy_Detected = 8,
            Vacancy_Detected = 9,
            IFTN_Status_Equ = 23,
            Away_Status_B = 24,
            Lamp_1_On_FB = 26,
            Lamp_1_Off_FB = 27
        }

        private enum eSignalSerialID
        {
            Multiplexed_Stamp = 2,
            Unit_Name = 3,
            Motion_Location = 4,
            Date = 5
        }

        private enum eSignalAnalogID
        {
            System_Mode = 2,
            Set_Setpoint_FB = 3,
            Local_Temp_FB_Scaled = 4,
            Resident_Status = 6,
            System_Status_FDMS = 8
        }

        class CustomDateTimeConverter : IsoDateTimeConverter
        {
            public CustomDateTimeConverter()
            {
                base.DateTimeFormat = "s";
            }
        }


        public class Property
        {
            public Dictionary<string, Room> rooms = new Dictionary<string, Room>();

            public void updateRoom(Room room)
            {
                if (rooms.ContainsKey(room.Name))
                {
                    rooms[room.Name] = room;
                }
                else{
                    rooms.Add(room.Name, room);
                }
            }

            public string toJson()
            {
                return JsonConvert.SerializeObject(this);
            }

            public Room getRoom(string roomName)
            {
                try {
                    return rooms[roomName];
                } catch (KeyNotFoundException)
                {
                    ErrorLog.Error("Room does not exist : {0}", roomName);
                    return null;
                }
            }
        }

        public class Room
        {
            public string Name { get; set; }
            public bool isOccupied { get; private set; }
            [JsonConverter(typeof(CustomDateTimeConverter))]
            public DateTime lastOccupied { get; private set; }

            public void setOccupied()
            {
                isOccupied = true;
                lastOccupied = DateTime.Now;
            }

            public void setVacant()
            {
                isOccupied = false;
            }

            public string toJson()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        public void TxPropertyStatus(Property property)
        {
            string status = property.toJson();
            CrestronConsole.PrintLine("[+] Sending Property Status :");
            CrestronConsole.PrintLine("[+]   {0}", status);

            // Send the status update to IoT
            iot.sendMessage(status);
        }

        public void TxRoomStatus(string roomName)
        {
            Room room = property.getRoom(roomName);

            if (room != null)
                CrestronConsole.PrintLine("[+] Sending Room Status : {0}", property.toJson());
            else
                CrestronConsole.PrintLine("[+] Room not found : {0}", roomName);
        }

        public void EICSignalHandler(BasicTriList currentDevice, SigEventArgs args)
        {
            CrestronConsole.PrintLine("[+] Signal Received : {0}", currentDevice.ToString());

            Sig signal = args.Sig;

            switch (signal.Type)
            {
                // Digital
                case (eSigType.Bool):
                    CrestronConsole.PrintLine("[+]   Digital: {0}", signal.ToString());

                    // Occupancy Detected Signal
                    if (signal.Number == (uint)eSignalDigitalID.Occupancy_Detected)
                    {
                        CrestronConsole.PrintLine("[+]   Occupancy Detected: {0}", signal.BoolValue);

                        if (signal.BoolValue)
                        {
                            // Start receiving occupancy information. New room object to store info.
                            _room = new Room();
                            _room.setOccupied();
                        }
                        else
                        {
                            // Done receiving occupancy information. Store the room state
                            property.updateRoom(_room);
                            TxPropertyStatus(property);
                        }
                    }

                    // Vacancy Detected Signal
                    if (signal.Number == (uint)eSignalDigitalID.Vacancy_Detected)
                    {
                        CrestronConsole.PrintLine("[+]   Vacancy Detected: {0}", signal.BoolValue);

                        if (signal.BoolValue)
                        {
                            // Start receiving vacancy information. New room object to store info.
                            _room = new Room();
                            _room.setVacant();
                        }
                        else
                        {
                            // Done receiving vacancy information. Store the room state
                            property.updateRoom(_room);
                        }
                    }

                    break;

                // Analog
                case (eSigType.UShort):
                    CrestronConsole.PrintLine("[+]   Analog: {0}", signal.ToString());

                    break;

                // Serial
                case (eSigType.String):
                    CrestronConsole.PrintLine("[+]   Serial: {0}", signal.ToString());

                    // Motion_Location$ Signal
                    if (signal.Number == (uint)eSignalSerialID.Motion_Location)
                    {
                        CrestronConsole.PrintLine("[+]   Motion Location: {0}", signal.StringValue);

                        _room.Name = signal.StringValue;
                    }

                    break;
                default:
                    CrestronConsole.PrintLine("[+]   Unknown: {0}", signal.ToString());

                    break;
            }
        }

        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// 
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        public override void InitializeSystem()
        {
            try
            {
                // Instantiate the Property object
                property = new Property();

                CrestronConsole.PrintLine("[+] Program Initiatilized");

                // Create an instance of AWsIoT to communicate with the cloud
                iot = new AWSIoT();
                CrestronConsole.PrintLine("[+] Starting iot client");
                iot.Start();

            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }


        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {

                    }
                    break;
                default:
                    CrestronConsole.PrintLine("[+] EthernetEvent: {0}", ethernetEventArgs.ToString());

                    break;
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
                default:
                    CrestronConsole.PrintLine("[+] ProgramEvent: {0}", programStatusEventType.ToString());

                    break;
            }

        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
                default:
                    CrestronConsole.PrintLine("[+] ControllerSystem: {0}", systemEventType.ToString());

                    break;
            }

        }
    }
}