// Assets/Scripts/Network/IPlayerFactory.cs
using Fusion;

namespace Game.Network
{
    public interface IPlayerFactory
    {
        NetworkObject Spawn(PlayerRef playerRef);
    }
}
