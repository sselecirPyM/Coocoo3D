using Coocoo3D.Core;
using System.IO;

namespace Coocoo3D.Windows;

class Program
{
    static void Main(string[] args)
    {
        Core.Coocoo3DMain coocoo3DMain = new Core.Coocoo3DMain();
        if (args.Length == 1)
        {
            string filename = args[0];
            if (filename != null)
            {
                coocoo3DMain.launchCallback += (context) =>
                {
                    context.GetSystem<SceneExtensionsSystem>().OpenFile(filename);
                };
            }
        }
        Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);

        coocoo3DMain.Launch();

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
