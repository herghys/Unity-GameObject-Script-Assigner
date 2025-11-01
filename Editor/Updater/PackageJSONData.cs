namespace Herghys.GameObjectScriptAssigner.Updater
{
    [System.Serializable]
    internal class PackageJsonData
    {
        public string name;
        public string version;
        public string displayName;
        public string description;
        public PackageAuthor author;
        public PackageRepository repository;

        public string repositoryUrl => repository != null ? repository.url : null;
    }

    [System.Serializable]
    internal class PackageAuthor
    {
        public string name;
        public string url;
    }

    [System.Serializable]
    internal class PackageRepository
    {
        public string type;
        public string url;
    }
}
