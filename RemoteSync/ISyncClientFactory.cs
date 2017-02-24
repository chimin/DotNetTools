namespace RemoteSync
{
    interface ISyncClientFactory
    {
        ISyncClient Create(string target);
    }
}