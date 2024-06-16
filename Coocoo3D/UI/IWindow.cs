namespace Coocoo3D.UI
{
    public interface IWindow
    {
        public virtual void OnGUI()
        {

        }
        public virtual void OnClose()
        {

        }
        public virtual void OnShow()
        {

        }
        public virtual void OnHide()
        {

        }
        public virtual string Title { get => null; }
        public virtual bool SimpleWindow { get => true; }
    }
}
