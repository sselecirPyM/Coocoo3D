namespace Coocoo3D.RenderPipeline;

public interface ISyncTask
{
    public void Process(object state);
}
public interface IAsyncTask
{
    public void Process(object state);

    public void SyncProcess(object state);
}
