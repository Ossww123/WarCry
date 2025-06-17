// Assets/Scripts/NetworkMessages.cs
using Mirror;
using System.Collections.Generic;

namespace NetworkMessages
{
    public struct PlayerListUpdateMessage : NetworkMessage
    {
        public List<uint> playerNetIds;
        public List<string> playerNames;
        public List<bool> playerReadyStates;
        public List<int> playerColorIndices; // 추가: 플레이어 색상 인덱스
        public List<bool> playerIsHost;      // 추가: 플레이어가 HOST인지 여부
    }
    public struct HostTransferMessage : NetworkMessage
    {
        public uint newHostNetId;
    }

    public struct PlayerSpawnRequest : NetworkMessage { }
}