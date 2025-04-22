using Fusion;
using System.Threading.Tasks;

namespace Game.Network
{
    public interface INetworkService
    {
        NetworkRunner Runner { get; }
        Task StartGame(GameMode mode);
    }
}
