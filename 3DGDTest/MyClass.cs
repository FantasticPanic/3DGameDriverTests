using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using gdio.unity_api.v2;
using gdio.unity_api;
using gdio.common.objects;
using System.Configuration;
using System.Diagnostics;
using System.Linq.Expressions;

namespace _3DGDTest
{
    public class MyClass
    {
        //IDE, standalone or Appium
        static string mode = "IDE";

        //IP address of test host. Use localhost for local
        //static string host 
        //must be run in standalone mode for Mobile
        static string host = "localhost";

        static string pathToExe = null;

        ApiClient api;

        //get the PID for later use
        int PID;


        public string testMode = TestContext.Parameters.Get("Mode", mode);
        public string testHost = TestContext.Parameters.Get("Host", host);
        public string executablePath = TestContext.Parameters.Get("executablePath", pathToExe);

        [OneTimeSetUp]
        public void Connect() 
        {
            api = new ApiClient();

            if (executablePath != null && testMode == "Standalone")
            {
                PID = ApiClient.Launch(executablePath);
                api.Wait(5000);
                Console.WriteLine($"Launching standalone executable wit PID: {PID}");
                api.Connect(testHost);

            }
            else if (executablePath == null && testMode == "IDE")
            {
                api.Connect(testHost, 19734, true, 30);
            }
            else
            {
                api.Connect(testHost, 19734, false, 30);
            }

            api.EnableHooks(HookingObject.KEYBOARD);
            api.EnableHooks(HookingObject.MOUSE);


            api.LoggedMessage += (s, e) =>
            {
                Console.WriteLine(e.Message);
            };

            api.UnityLoggedMessage += (s, e) =>
            {
                Console.WriteLine($"Type: {e.type.ToString()}\r\nCondition: {e.condition}\r\nStackTrace: {e.stackTrace}");
            };

            //check if we are at the Start scene
            if (api.GetSceneName() == "Start")
            {
                api.WaitForObject("//*[@name= 'StartButton']");
                api.ClickObject(MouseButtons.LEFT, "//*[@name= 'StartButton']", 30);
                api.Wait(10000);
                api.WaitForObject("//*[@name= 'Ellen']");
            }
            //assert we are in Level 1
            ClassicAssert.AreEqual("Level1", api.GetSceneName());
        }

        //Stop Unity editor and stop game  
        [OneTimeTearDown]
        public void Disconnect()
        {
            api.Wait(2000);

            api.DisableHooks(HookingObject.ALL);
            api.Wait(2000);

            api.Disconnect();

            if (testMode == "IDE")
            {
                api.Wait(2000);
                api.StopEditorPlay();
            }
            else if (testMode == "Standalone")
            {
                ApiClient.TerminateGame();
            }
        }

        [Test, Order(1)]
        public void TestMovementInputs()
        {
            ClassicAssert.IsTrue(api.GetSceneName() == "Level1", "Wrong Scene!");

            api.WaitForObject("//*[@name= 'Ellen']");
            api.Wait(3000);
            Vector3 ellenPos = api.GetObjectPosition("//*[@name= 'Ellen']");
            Console.WriteLine($"Original position is:" + ellenPos.ToString());

            var fps = (ulong)api.GetLastFPS();

            api.AxisPress("Horizontal", 1f, fps * 3);
            api.Wait(1000);
            api.AxisPress("Vertical", 1f, fps * 3);
            api.Wait(1000);
            api.AxisPress("Horizontal", -1f, fps * 3);
            api.Wait(1000);
            api.AxisPress("Vertical", -1f, fps * 3);
            api.Wait(1000);

            Vector3 newPos = api.GetObjectPosition("//*[@name= 'Ellen']");
            Console.WriteLine($"New position is:" + newPos.ToString());

            ClassicAssert.AreNotEqual(ellenPos, newPos, "Ellen didn't move!");
        }
        
        [Test, Order(2)]
        public void TestCameraMovement()
        {
            api.Wait(3000);
            api.WaitForObject("//*[@name= 'Ellen']");

            Vector3 initialCameraPos = api.GetObjectPosition("//MainCamera[@name= 'CameraBrain']");

            var fps = (ulong)api.GetLastFPS();

            api.AxisPress("CameraX", 1f, fps * 2);
            api.AxisPress("CameraY", 1f, fps * 2);
            api.Wait(5000);

            Vector3 newCameraPos = api.GetObjectPosition("//MainCamera[@name= 'CameraBrain']");

            ClassicAssert.AreNotEqual(newCameraPos, initialCameraPos, "Camera didn't move!");

        }

        //check pause menu
        [Test, Order(3)]
        public void TestMenu()
        {
            api.ButtonPress("Pause", 30, 30);
            api.Wait(1000);

            ClassicAssert.IsTrue(api.GetObjectFieldValue<bool>("//*[@name='PauseCanvas']", "active"), "Menu didn't appear!");

            api.ButtonPress("Pause", 30, 30);
            api.Wait(3000);
        }

        [Test, Order(4)]
        public void GetWeaponToEnableAttacks()
        {   
            //If weapon is active, go get it 
            if (api.GetObjectFieldValue<bool>("(//*[@name='Staff'])[1]/@active") == true)
            { 
                //Move to the staff to enable melle attacks
                api.SetObjectFieldValue($"//*[@name='Ellen']/fn:component('UnityEngine.Transform')", "position", 
                    api.GetObjectPosition("(//*[@name= 'Staff'])[1]", CoordinateConversion.None));
                api.Wait(1000);
            }

            //Check that we can attack now
            ClassicAssert.IsTrue(api.GetObjectFieldValue<bool>("/Untagged[@name='Ellen']/fn:component('Gamekit3D.PlayerController')/@canAttack"),
                "Melee not enabled!");
        }

        //kill all the chomper enemies in the level
        [Test, Order(5)]
        public void KillAllChompers()
        {
            api.Wait(5000);
            var objectList = api.GetObjectList("//*[@name='Chomper']", true, 60);
            int enemyCount = 0;

            foreach (var obj in objectList)
            {
                if (obj.Name == "Chomper")
                {
                    enemyCount++;
                }
            }

            api.CallMethod("//*[@name='Ellen']/fn:component('Gamekit3D.Damageable')", "SetColliderState", new object[] { false });
            //If the melee attack isn't enabled, enable it
            if (api.GetObjectFieldValue<bool>("/Untagged[@name='Ellen']/fn:component('Gamekit3D.PlayerController')/@canAttack") == false)
            {
                //Enable melee attack by calling the method
                api.CallMethod("/Untagged[@name='Ellen']/fn:component('Gamekit3D.PlayerController')", "SetCanAttack", new object[] { true });
            }

            try
            {

                while (enemyCount > 0)
                {
                    Vector3 dest = CloseToObject("//*[@name='Chomper']");
                    Vector3 target = api.GetObjectPosition("//*[@name='Chomper']", CoordinateConversion.None);

                    SetObjectPosition("//*[@name = 'Ellen']", dest);

                    //Look at the target object
                    api.CallMethod("//*[@name = 'Ellen']/fn:component('UnityEngine.Transform')", "LookAt", new Vector3[] { target });
                    api.ButtonPress("Fire1", 30, 30);
                    api.Wait(1000);
                    enemyCount--;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
                ClassicAssert.IsTrue(enemyCount == 0, "We missed one!");
                api.CallMethod("//*[@name='Ellen']/fn:component('Gamekit3D.Damageable')", "SetColliderState", new object[] { true });


        }

        //player respawns after falling in water
        [Test, Order(6)]
        public void TestPlayerRespawnAfterFalling()
        {
            Vector3 outOfBounds = new Vector3(50, -2, 29);
  

            SetObjectPosition("//*[@name = 'Ellen']", outOfBounds);
            api.Wait(10000);

            ClassicAssert.IsFalse(api.GetObjectPosition("//*[@name = 'Ellen']").y < 0, "Player has not respawned");
        }

        //
        [Test, Order(7)]
        public void TestPressurePadOpens_Level1_Door1()
        {
            Vector3 padPos = api.GetObjectPosition("/*[@name='Level01Gameplay']/*[@name='PressurePad1']");
            padPos.y += 5;

            SetObjectPosition("//*[@name = 'Ellen']", padPos);
            api.Wait(3000);

            ClassicAssert.IsTrue(api.GetObjectFieldValue<bool> ("/*[@name='Level01Gameplay']/*[@name='DoorHuge1']" +
                "                                               /fn:component('Gamekit3D.GameCommands.SimpleTranslator')/@activate"), "Pressure pad did not open door");
        }

        [Test, Order(8)]
        public void TestPressurePadOpens_Level1_Door2()
        {
            var objectList = api.GetObjectList("//*[@name='Switch']", true, 60);
            int switchCount = 0;

            foreach  (var obj in objectList)
            {
                Vector3 dest = obj.Position;
                SetObjectPosition("//*[@name = 'Ellen']", dest);
                api.Wait(1000);
            }

            
            api.Wait(5000);
            ClassicAssert.IsTrue(api.GetObjectFieldValue<bool>("/*[@name='Level01Gameplay']/*[@name='DoorHuge']" +
                "                                               /fn:component('Gamekit3D.GameCommands.SimpleTranslator')/@activate"), "Pressure pad did not open door");
        }


        [Test, Order(9)]
        public void TestGrenadierSpawn()
        {
            api.Wait(5000);
            var objectList = api.GetObjectList("//*[@name='Grenadier']", true, 60);
            int enemyCount = 0;

            foreach (var obj in objectList)
            {
                if (obj.Name == "Grenadier")
                {
                    enemyCount++;
                }
            }

            api.CallMethod("//*[@name='Ellen']/fn:component('Gamekit3D.Damageable')", "SetColliderState", new object[] { false });
            //If the melee attack isn't enabled, enable it
            if (api.GetObjectFieldValue<bool>("/Untagged[@name='Ellen']/fn:component('Gamekit3D.PlayerController')/@canAttack") == false)
            {
                //Enable melee attack by calling the method
                api.CallMethod("/Untagged[@name='Ellen']/fn:component('Gamekit3D.PlayerController')", "SetCanAttack", new object[] { true });
            }

            try
            {

                while (enemyCount > 0)
                {
                    Vector3 dest = CloseToObject("//*[@name='Grenadier']");
                    Vector3 target = api.GetObjectPosition("//*[@name='Grenadier']", CoordinateConversion.None);

                    SetObjectPosition("//*[@name = 'Ellen']", dest);
                    api.Wait(300);

                    //Look at the target object
                    api.CallMethod("//*[@name = 'Ellen']/fn:component('UnityEngine.Transform')", "LookAt", new Vector3[] { target });
                    api.Wait(500);

                    api.ButtonPress("Fire1", 30, 30);
                    api.Wait(1000);
                    enemyCount--;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            ClassicAssert.IsTrue(enemyCount == 0, "We missed one!");
            api.CallMethod("//*[@name='Ellen']/fn:component('Gamekit3D.Damageable')", "SetColliderState", new object[] { true });


        }

        [Test, Order(10)]
        public void TestTransitionToLevel2()
        {
            Vector3 dest = CloseToObject("//*[@name='TeleporterVolume']");
            Vector3 target = api.GetObjectPosition("//*[@name='TeleportPlane']");
            
            SetObjectPosition("//*[@name = 'Ellen']", dest);
            api.Wait(300);
            
            //Look at the door object
            api.CallMethod("//*[@name = 'Ellen']/fn:component('UnityEngine.Transform')", "LookAt", new Vector3[] { target });
            api.Wait(500);

            //Go through the Level 2 door
            api.AxisPress("Horizontal", -1f, (ulong)api.GetLastFPS() * 3);
            api.Wait(1000);

            //allow the scene time to load
            api.WaitForObject("//*[@name='Level02Dressing']");


            ClassicAssert.AreEqual("Level2", api.GetSceneName(), "Wrong zone!");
        }

        public void SetObjectPosition(string Hpath, Vector3 pos)
        {
            api.SetObjectFieldValue($"{Hpath}/fn:component('UnityEngine.Transform')", "position", pos);
        }

         Vector3 CloseToObject(string Hpath)
        {
            Vector3 initialPos = api.GetObjectPosition(Hpath);
            Vector3 returnPos = new Vector3(initialPos.x - .5f, initialPos.y, initialPos.z - .5f);
            return returnPos;
        }
    }
}
