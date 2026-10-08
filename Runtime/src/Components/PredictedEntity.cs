// Copyright (c) 2026 Milorad Liviu Felix
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using UnityEngine;

namespace Sector0.Ursitoare.Components
{
    public interface PredictedEntity
    {
        uint GetId();
        int GetOwnerId();
        ClientPredictedEntity GetClientEntity();
        ServerPredictedEntity GetServerEntity();
        PredictedEntityVisuals GetVisualsControlled();
        bool IsServer();
        bool IsClient();
        bool IsClientOnly()
        {
            return IsClient() && !IsServer();
        }
        Rigidbody GetRigidbody();
        
        void Register()
        {
            if (IsServer())
            {
                ServerPredictionManager.Instance.AddPredictedEntity(GetServerEntity());
            }
            else if (IsClient())
            {
                ClientPredictionManager.Instance.AddPredictedEntity(GetClientEntity());
            }
        }

        void Deregister()
        {
            if (IsServer())
            {
                ServerPredictionManager.Instance.RemovePredictedEntity(GetServerEntity());
            }
            else if (IsClient())
            {
                ClientPredictionManager.Instance.RemovePredictedEntity(GetClientEntity());
            }
        }
    }
}
