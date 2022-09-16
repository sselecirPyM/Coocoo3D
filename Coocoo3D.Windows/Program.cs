namespace Coocoo3D.Windows
{
    class Program
    {
        static void Main(string[] args)
        {
            Core.Coocoo3DMain coocoo3DMain = new Core.Coocoo3DMain();
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
}
