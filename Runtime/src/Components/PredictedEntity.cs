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
                ServerPredictionManager.RegisterEntity(GetServerEntity());
            }
            else if (IsClient())
            {
                ClientPredictionManager.RegisterEntity(GetClientEntity());
            }
        }

        void Deregister()
        {
            if (IsServer())
            {
                ServerPredictionManager.UnregisterEntity(GetServerEntity());
            }
            else if (IsClient())
            {
                ClientPredictionManager.UnregisterEntity(GetClientEntity());
            }
        }
    }
}
