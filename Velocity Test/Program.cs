using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using Thorlabs.MotionControl.GenericMotorCLI;
using Thorlabs.MotionControl.GenericMotorCLI.Settings;
using Thorlabs.MotionControl.KCube.DCServoCLI;
using Thorlabs.MotionControl.DeviceManagerCLI;
using Thorlabs.MotionControl.GenericMotorCLI.AdvancedMotor;
using System.IO;
using System.Timers;

namespace velocityTest
{

    class Program
    {
        // Simulation? //
        static bool SimulationTrue = true;

        static double velocity = 0;

        static StreamWriter writer;
        static KCubeDCServo device1, device2, device3;

        static void Main(string[] args)
        {

            string serialNo1 = "27505282"; // x
            string serialNo2 = "27505360"; // y
            string serialNo3 = "27505370"; // z

            //Simulation
            if (SimulationTrue == true)
            {
                SimulationManager.Instance.InitializeSimulations();
                serialNo1 = "27000001"; // x
                serialNo2 = "27000002"; // y
                serialNo3 = "27000003"; // z
            }

            DeviceManagerCLI.BuildDeviceList();

            device1 = KCubeDCServo.CreateKCubeDCServo(serialNo1);
            device2 = KCubeDCServo.CreateKCubeDCServo(serialNo2);
            device3 = KCubeDCServo.CreateKCubeDCServo(serialNo3);

            Console.WriteLine("Opening devices...");
            device1.Connect(serialNo1);
            device2.Connect(serialNo2);
            device3.Connect(serialNo3);

            device1.WaitForSettingsInitialized(5000);
            device2.WaitForSettingsInitialized(5000);
            device3.WaitForSettingsInitialized(5000);

            device1.LoadMotorConfiguration(device1.DeviceID, DeviceConfiguration.DeviceSettingsUseOptionType.UseFileSettings);
            device2.LoadMotorConfiguration(device2.DeviceID, DeviceConfiguration.DeviceSettingsUseOptionType.UseFileSettings);
            device3.LoadMotorConfiguration(device3.DeviceID, DeviceConfiguration.DeviceSettingsUseOptionType.UseFileSettings);

            device1.StartPolling(250);
            device2.StartPolling(250);
            device3.StartPolling(250);

            device1.EnableDevice();
            device2.EnableDevice();
            device3.EnableDevice();
            Console.WriteLine("Devices Enabled");

            Thread.Sleep(500);

            Console.WriteLine("Now Homing...");
            Thread Home1Thread = new Thread(() => Home1(device1));
            Thread Home2Thread = new Thread(() => Home2(device2));
            Thread Home3Thread = new Thread(() => Home3(device3));

            if (SimulationTrue == false)
            {
                Home1Thread.Start();
                Home2Thread.Start();
                Home3Thread.Start();
                Home1Thread.Join();
                Home2Thread.Join();
                Home3Thread.Join();
            }
            

            Console.WriteLine("Devices Homed.");

            // Prepare the CSV writer
            writer = new StreamWriter("VelocityData.csv");
            writer.WriteLine("Time (s),X Position,Y Position,Z Position");

            string response = "none";

            while (true) 
            {
                Console.WriteLine("Choose a velocity or write 'break' to end:");
                response = Console.ReadLine();

                if (response == "break")
                {
                    Console.WriteLine("Ending test...");
                    break;
                }
                
                writer.WriteLine($"Velocity: {velocity}");

                velocity = Convert.ToDouble(response);
                Console.WriteLine("Moving to start position...");

                Thread MoveHome = new Thread(() => MoveAct(device2, 5, 10));
                MoveHome.Start();
                MoveHome.Join();

                Console.WriteLine("Starting movement...");
                Stopwatch sw = Stopwatch.StartNew();

                // Start capturing positions while the device is moving
                Thread capturePositionsThread = new Thread(CapturePositionsDuringMovement);
                capturePositionsThread.Start();

                Thread MoveEnd = new Thread(() => MoveAct(device2, 25, velocity));
                MoveEnd.Start();
                MoveEnd.Join();

                // Stop capturing positions after the movement ends
                capturePositionsThread.Join();

                sw.Stop();
                Console.WriteLine($"Time taken: {sw.ElapsedMilliseconds} ms");
                Console.WriteLine($"Velocity: {velocity}");
            }

            // Close the CSV writer
            writer.Close();

            // Stop polling and disconnect devices
            device1.StopPolling();
            device2.StopPolling();
            device3.StopPolling();
            device1.ShutDown();
            device2.ShutDown();
            device3.ShutDown();

            Console.WriteLine("Test is over. Press any key to exit.");
            Console.ReadKey();

            if (SimulationTrue == true)
            {
                SimulationManager.Instance.UninitializeSimulations();
            }

        }

        static void CapturePositionsDuringMovement()
        { 
            Stopwatch movementStopwatch = Stopwatch.StartNew();

            double timeToMove = 20 / velocity * 1000;

            while (movementStopwatch.ElapsedMilliseconds<timeToMove)
            {
                while((movementStopwatch.ElapsedMilliseconds < 2000) || ((movementStopwatch.ElapsedMilliseconds > (timeToMove - 2000))&& movementStopwatch.ElapsedMilliseconds < timeToMove))
                {
                    // Capture the current positions
                    decimal xPos = device1.Position;
                    decimal yPos = device2.Position;
                    decimal zPos = device3.Position;

                    // Get the elapsed time in seconds
                    double time = movementStopwatch.ElapsedMilliseconds / 1000.0;

                    // Write the data to the CSV file
                    writer.WriteLine($"{time},{xPos},{yPos},{zPos}");
                    Console.WriteLine($"Time: {time}s, X: {xPos}, Y: {yPos}, Z: {zPos}");

                    // Wait 0.1 seconds before capturing the next position
                    Thread.Sleep(1);
                }
            }
        }

        static void MoveAct(KCubeDCServo device, decimal Zposition, double Velocities)
        {
            device.SetVelocityParams(acceleration: 100, maxVelocity: Convert.ToDecimal(Velocities));
            device.MoveTo(Zposition, 200000);
            Console.WriteLine($"Current position: {device.Position}");
        }

        static void Home1(KCubeDCServo device)
        {
            device.Home(60000);
        }

        static void Home2(KCubeDCServo device)
        {
            device.Home(60000);
        }

        static void Home3(KCubeDCServo device)
        {
            device.Home(60000);
        }
    }
}
