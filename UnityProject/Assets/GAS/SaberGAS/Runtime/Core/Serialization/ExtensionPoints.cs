using System.Collections.Generic;

namespace Saber.GAS.Serialization
{
    public interface IDeepCloneProvider
    {
        T Clone<T>(T source);
    }

    public interface IStateSerializer
    {
        byte[] Serialize<T>(T source);

        T Deserialize<T>(byte[] payload);
    }

    public interface IJsonAdapter
    {
        string SerializeObject(object source);

        T DeserializeObject<T>(string json);
    }

    public interface IAbilityPayload
    {
        IReadOnlyDictionary<string, object> Fields { get; }
    }
}
