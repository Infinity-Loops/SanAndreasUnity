﻿using System.Collections.Generic;
using UnityEngine;
using Mirror;
using SanAndreasUnity.Behaviours;
using SanAndreasUnity.Utilities;
using System.Linq;

namespace SanAndreasUnity.Net
{

    public class Player : NetworkBehaviour
    {

        static List<Player> s_allPlayers = new List<Player>();
        public static Player[] AllPlayersCopy => s_allPlayers.ToArray();
        public static IEnumerable<Player> AllPlayersEnumerable => s_allPlayers;
        public static IReadOnlyList<Player> AllPlayersList => s_allPlayers;

        /// <summary>Local player.</summary>
        public static Player Local { get; private set; }

        public static event System.Action<Player> onStart = delegate {};
        public static event System.Action<Player> onDisable = delegate {};

        [SyncVar(hook=nameof(OnOwnedGameObjectChanged))] GameObject m_ownedGameObject;
        Ped m_ownedPed;
        //public GameObject OwnedGameObject { get { return m_ownedGameObject; } internal set { m_ownedGameObject = value; } }
        public Ped OwnedPed { get { return m_ownedPed; } internal set { m_ownedPed = value; m_ownedGameObject = value != null ? value.gameObject : null; } }

        public string DescriptionForLogging => "(netId=" + this.netId + ", addr=" + (this.connectionToClient != null ? this.connectionToClient.address : "") + ")";

        private readonly SyncedBag.StringSyncDictionary m_syncDictionary = new SyncedBag.StringSyncDictionary();
        public SyncedBag ExtraData { get; }


        Player()
        {
            ExtraData = new SyncedBag(m_syncDictionary);
        }

        public static Player GetOwningPlayer(Ped ped)
        {
            if (null == ped)
                return null;
            return AllPlayersEnumerable.FirstOrDefault(p => p.OwnedPed == ped);
        }

        void OnEnable()
        {
            s_allPlayers.Add(this);
        }

        void OnDisable()
        {
            s_allPlayers.Remove(this);

            // kill player's ped
            if (NetStatus.IsServer)
            {
                if (this.OwnedPed)
                    Destroy(this.OwnedPed.gameObject);
            }

            F.InvokeEventExceptionSafe(onDisable, this);

        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (this.isServer)
                return;
            
            m_ownedPed = m_ownedGameObject != null ? m_ownedGameObject.GetComponent<Ped>() : null;
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            Local = this;
        }

        void Start()
        {
            // log some info
            if (!this.isLocalPlayer)
                Debug.LogFormat("Player {0} connected, time: {1}", this.DescriptionForLogging, F.CurrentDateForLogging);

            F.InvokeEventExceptionSafe(onStart, this);
        }

        public override void OnNetworkDestroy()
        {
            base.OnNetworkDestroy();
            
            // log some info about this
            if (!this.isLocalPlayer)
                Debug.LogFormat("Player {0} disconnected, time: {1}", this.DescriptionForLogging, F.CurrentDateForLogging);
        }

        void OnOwnedGameObjectChanged(GameObject newGo)
        {
            Debug.LogFormat("Owned game object changed for player (net id {0})", this.netId);

            if (this.isServer)
                return;

            m_ownedGameObject = newGo;

            m_ownedPed = m_ownedGameObject != null ? m_ownedGameObject.GetComponent<Ped>() : null;
        }

        void Update()
        {

            // Telepathy does not detect dead connections, so we'll have to detect them ourselves
            if (NetStatus.IsServer && !this.isLocalPlayer)
            {
                if (Time.time - this.connectionToClient.lastMessageTime > 6f)
                {
                    // disconnect client
                    Debug.LogFormat("Detected dead connection for player {0}", this.DescriptionForLogging);
                    this.connectionToClient.Disconnect();
                }
            }

        }

        public void Disconnect()
        {
            this.connectionToClient.Disconnect();
        }

        public static Player GetByNetId(uint netId)
        {
            var go = NetManager.GetNetworkObjectById(netId);
            return go != null ? go.GetComponent<Player>() : null;
        }

    }

}
