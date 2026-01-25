using UnityEngine;

namespace Prediction.data
{
    public class PhysicsStateRecord
    {
        public uint tickId;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public PredictionInputRecord input;
        //Components have relevant state to be received from the server
        public PredictionInputRecord componentState;
        
        //NOTE: DO NOT USE THE DEFAULT CONSTRUCTOR. DIDNT MAKE IT PRIVATE SO MIRROR CAN SERIALIZE THIS ENTITY
        
        public static PhysicsStateRecord AllocWithComponentState(int componentFloats, int componentBools)
        {
            PhysicsStateRecord psr = Empty();
            psr.componentState = new PredictionInputRecord(componentFloats, componentBools);
            return psr;
        }

        public static PhysicsStateRecord Alloc()
        {
            return Empty();
        }
        
        public static PhysicsStateRecord Empty()
        {
            PhysicsStateRecord psr = new PhysicsStateRecord();
            psr.tickId = 0;
            psr.position = Vector3.zero;
            psr.rotation = Quaternion.identity;
            psr.velocity = Vector3.zero;
            psr.angularVelocity = Vector3.zero;
            psr.input = null;
            psr.componentState = null;
            return psr;
        }

        public void From(Rigidbody rigidbody)
        {
            position = rigidbody.position;
            rotation = rigidbody.rotation;
            velocity = rigidbody.linearVelocity;
            angularVelocity = rigidbody.angularVelocity;
        }

        public void From(PhysicsStateRecord record)
        {
            tickId = record.tickId;
            position = record.position;
            rotation = record.rotation;
            velocity = record.velocity;
            angularVelocity = record.angularVelocity;
            if (input != null && record.input != null)
            {
                input.From(record.input);
            }

            if (componentState != null && record.componentState != null)
            {
                componentState.From(record.componentState);
            }
        }

        public void To(Rigidbody r)
        {
            r.position = position;
            r.rotation = rotation;
            r.linearVelocity = velocity;
            r.angularVelocity = angularVelocity;
        }
        
        public void From(PhysicsStateRecord record, uint tickOverride)
        {
            From(record);
            tickId = tickOverride;
        }

        public override string ToString()
        {
            return $"t:{tickId} p:{position.ToString("F10")} r:{rotation.ToString("F10")} v:{velocity.ToString("F10")} ang:{angularVelocity.ToString("F10")} input:{input}";
        }
        
        public override bool Equals(object obj)
        {
            var other = obj as PhysicsStateRecord;

            if (other == null)
            {
                return false;
            }

            return tickId == other.tickId 
                   && position == other.position 
                   && rotation == other.rotation 
                   && velocity == other.velocity 
                   && angularVelocity == other.angularVelocity;
        }

        public override int GetHashCode()
        {
            return (int)tickId + position.GetHashCode() + rotation.GetHashCode() + velocity.GetHashCode() + angularVelocity.GetHashCode();
        }
    }
}