using System;
using Game.Foundation;
using UnityEngine;

namespace Game.Infrastructure
{
    public sealed class BuildInfoProvider : IBuildInfo
    {
        private readonly BuildInfoData data;

        public BuildInfoProvider()
        {
            var asset = Resources.Load<TextAsset>("Generated/build-info");
            data = asset == null ? BuildInfoData.Fallback() : JsonUtility.FromJson<BuildInfoData>(asset.text);
            if (data == null)
            {
                data = BuildInfoData.Fallback();
            }
        }

        public string Version => data.version;
        public string BuildNumber => data.buildNumber;
        public string Commit => data.commit;
        public string Channel => data.channel;
        public string UnityVersion => data.unityVersion;

        [Serializable]
        private sealed class BuildInfoData
        {
            public string version;
            public string buildNumber;
            public string commit;
            public string channel;
            public string unityVersion;

            public static BuildInfoData Fallback() => new BuildInfoData
            {
                version = Application.version,
                buildNumber = "local",
                commit = "working-tree",
                channel = "local",
                unityVersion = Application.unityVersion
            };
        }
    }
}
