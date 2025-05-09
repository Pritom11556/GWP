using UnityEngine;
// using Mirror; // Or your preferred networking library
using System.Collections.Generic;

public enum EntityType
{
    Player,
    NPC,
    Projectile,
    InteractableObject,
    Vehicle
}

// Base class for network-synchronized entities
// In a real Mirror/Netcode setup, this would likely inherit from NetworkBehaviour
public abstract class NetworkedEntity : MonoBehaviour 
{
    // [SyncVar] // Example for Mirror to automatically sync this variable
    public uint netId; // Unique network identifier for this entity
    public EntityType entityType;
    public string ownerPlayerId; // PlayerId of the owner/controller, if applicable

    public virtual void Initialize(uint id, EntityType type, string ownerId = null)
    {
        this.netId = id;
        this.entityType = type;
        this.ownerPlayerId = ownerId;
        EntitySyncSystem.Instance?.RegisterEntity(this);
    }

    protected virtual void OnDestroy()
    {
        EntitySyncSystem.Instance?.UnregisterEntity(this);
    }

    // Abstract methods for serialization and deserialization of entity state
    // These would be called by the EntitySyncSystem to gather/apply state updates.
    // The format of 'data' would be specific to your chosen serialization method (e.g., byte[], JSON string).
    public abstract object SerializeState(); // Server: Gathers state to send to clients
    public abstract void DeserializeState(object data); // Client: Applies state received from server

    // For client-side prediction and server reconciliation, you might add:
    // public abstract void ApplyInput(PlayerInput input); // Server: Processes input
    // public abstract void PredictState(PlayerInput input, float deltaTime); // Client: Predicts next state
    // public abstract void ReconcileState(object serverState); // Client: Corrects prediction with server state
}

public class EntitySyncSystem : MonoBehaviour
{
    public static EntitySyncSystem Instance { get; private set; }

    [Header("Synchronization Settings")]
    public float syncInterval = 0.1f; // How often to send updates (e.g., 10 times per second)

    private Dictionary<uint, NetworkedEntity> registeredEntities = new Dictionary<uint, NetworkedEntity>();
    private uint nextNetId = 1;
    private float lastSyncTime = 0f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // This system would typically only run on the server, or have server-specific logic
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsServer())
        {
            if (Time.time - lastSyncTime >= syncInterval)
            {
                SynchronizeEntities();
                lastSyncTime = Time.time;
            }
        }
    }

    public void RegisterEntity(NetworkedEntity entity)
    {
        if (entity.netId == 0) // Assign new netId if not already set (e.g., for server-spawned entities)
        {
            entity.netId = GetNextNetId();
        }
        
        if (!registeredEntities.ContainsKey(entity.netId))
        {
            registeredEntities.Add(entity.netId, entity);
            Debug.Log($"Entity registered: ID {entity.netId}, Type {entity.entityType}");
        }
        else
        {
            Debug.LogWarning($"Attempted to register entity with existing ID: {entity.netId}");
        }
    }

    public void UnregisterEntity(NetworkedEntity entity)
    {
        if (registeredEntities.ContainsKey(entity.netId))
        {
            registeredEntities.Remove(entity.netId);
            Debug.Log($"Entity unregistered: ID {entity.netId}");
        }
    }

    public NetworkedEntity GetEntity(uint netId)
    {
        registeredEntities.TryGetValue(netId, out NetworkedEntity entity);
        return entity;
    }

    private uint GetNextNetId()
    {
        // Basic incrementing ID. In a robust system, consider pooling or more complex ID management.
        return nextNetId++;
    }

    // --- Server-Side Synchronization Logic ---
    private void SynchronizeEntities()
    {
        if (NetworkManager.Instance == null || !NetworkManager.Instance.IsServer()) return;

        List<object> allEntitiesStateData = new List<object>();

        foreach (NetworkedEntity entity in registeredEntities.Values)
        {
            // TODO: Implement interest management (only send updates for entities relevant to each client)
            object entityState = entity.SerializeState();
            if (entityState != null)
            {
                // Package state with netId for identification on client
                // This structure depends heavily on your network message format
                var statePacket = new { NetId = entity.netId, State = entityState }; 
                allEntitiesStateData.Add(statePacket);
            }
        }

        if (allEntitiesStateData.Count > 0)
        {
            // TODO: Send 'allEntitiesStateData' to all connected clients
            // This would involve serializing 'allEntitiesStateData' into a byte array or similar
            // and using NetworkManager to broadcast it.
            // Example: NetworkManager.Instance.BroadcastMessage(MessageType.EntitySync, serializedData);
            // For now, just log it:
            // Debug.Log($"Server: Broadcasting state for {allEntitiesStateData.Count} entities.");
        }
    }

    // --- Client-Side State Update Handling ---
    // This method would be called by the NetworkManager when an entity sync message is received from the server.
    public void OnReceiveEntitySyncData(object receivedData) // 'receivedData' would be the deserialized list of state packets
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsClient())
        {
            // Assuming receivedData is a List of state packets like { NetId, State }
            List<object> entityStates = receivedData as List<object>; 
            if (entityStates == null) {
                // Try to cast to a more specific type if your networking layer provides it
                // For example, if it's a List of a specific class/struct:
                // List<EntityStatePacket> typedEntityStates = receivedData as List<EntityStatePacket>;
                // if(typedEntityStates != null) { /* process typedEntityStates */ }
                Debug.LogError("EntitySyncSystem: Received data in unexpected format.");
                return;
            }

            foreach (var statePacketObj in entityStates)
            {
                // This part is highly dependent on how statePacket is structured and serialized
                // Example using reflection if it's an anonymous type (not recommended for performance):
                try {
                    var netIdProperty = statePacketObj.GetType().GetProperty("NetId");
                    var stateProperty = statePacketObj.GetType().GetProperty("State");

                    if (netIdProperty != null && stateProperty != null) {
                        uint netId = (uint)netIdProperty.GetValue(statePacketObj);
                        object entityStateData = stateProperty.GetValue(statePacketObj);

                        NetworkedEntity entity = GetEntity(netId);
                        if (entity != null)
                        {
                            entity.DeserializeState(entityStateData);
                        }
                        else
                        {
                            // Entity doesn't exist locally, might need to be spawned
                            // This requires more complex logic: knowing entity type to spawn, etc.
                            // Debug.LogWarning($"Client: Received state for unknown entity ID {netId}. Spawning logic needed.");
                        }
                    }
                } catch (System.Exception e) {
                    Debug.LogError($"Error processing entity state packet: {e.Message}");
                }
            }
        }
    }
}

// Example of a concrete NetworkedEntity (e.g., for a simple movable cube)
public class SyncedCube : NetworkedEntity
{
    // [SyncVar] // In Mirror, this would sync automatically
    public Vector3 currentPosition;
    // [SyncVar]
    public Quaternion currentRotation;

    void Awake()
    {
        // Initialize with a default type, actual type might be set by spawner
        base.entityType = EntityType.InteractableObject; 
    }

    void Update()
    {
        // On the server or if this client has authority, update currentPosition/Rotation from transform
        if (NetworkManager.Instance != null && (NetworkManager.Instance.IsServer() /* || HasAuthority() */) )
        {
            currentPosition = transform.position;
            currentRotation = transform.rotation;
        }
        else // On clients without authority, apply the synced state
        {
            transform.position = Vector3.Lerp(transform.position, currentPosition, Time.deltaTime * 10f); // Smooth interpolation
            transform.rotation = Quaternion.Slerp(transform.rotation, currentRotation, Time.deltaTime * 10f);
        }
    }

    public override object SerializeState()
    {
        // Return a simple struct or class that holds the state
        return new CubeState { position = transform.position, rotation = transform.rotation };
    }

    public override void DeserializeState(object data)
    {
        if (data is CubeState state)
        {
            // For clients, these become the target values for interpolation
            currentPosition = state.position;
            currentRotation = state.rotation;
        }
    }

    // Helper struct for serialization
    [System.Serializable] // Make serializable if you use Unity's JsonUtility or similar
    public struct CubeState
    {
        public Vector3 position;
        public Quaternion rotation;
    }
}