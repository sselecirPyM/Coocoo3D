using System.IO;

namespace Coocoo3D.Windows;

class Program
{
    static void Main(string[] args)
    {
        Core.LaunchOption launchOption = new Core.LaunchOption();
        launchOption.AddLight = true;
        if (args.Length == 1)
        {
            launchOption.openFile = args[0];
            if (launchOption.openFile.EndsWith(".coocoo3DScene", System.StringComparison.CurrentCultureIgnoreCase))
                launchOption.AddLight = false;
        }
        Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
        Core.Coocoo3DMain coocoo3DMain = new Core.Coocoo3DMain(launchOption);
        WindowSystem windowSystem = new WindowSystem();
        windowSystem.coocoo3DMain = coocoo3DMain;
        windowSystem.Initialize();
        while (!windowSystem.quitRequested)
        {
            windowSystem.Update();
        }
        coocoo3DMain.Dispose();
    }
}
