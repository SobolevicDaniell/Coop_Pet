using Fusion;

namespace Game.Network
{
    public interface IPlayerFactory
    {
         NetworkObject Spawn(PlayerRef playerRef);
    }
}
