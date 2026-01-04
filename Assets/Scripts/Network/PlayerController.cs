using UnityEngine;
using Fusion;
using MLBShowdown.Core;

namespace MLBShowdown.Network
{
    public class PlayerController : NetworkBehaviour
    {
        [Networked] public TeamType AssignedTeam { get; set; }
        [Networked] public NetworkBool IsReady { get; set; }
        [Networked] public int TeamAssignmentRoll { get; set; }

        public override void Spawned()
        {
            if (Object.HasInputAuthority)
            {
                // This is the local player
                Debug.Log($"Local player spawned: {Object.InputAuthority}");
                
                // Find the networked game manager and notify we're ready
                StartCoroutine(NotifyGameManagerWhenReady());
            }
        }
        
        private System.Collections.IEnumerator NotifyGameManagerWhenReady()
        {
            // Wait for networked game manager to be available
            NetworkGameManager networkedGM = null;
            float timeout = 5f;
            float elapsed = 0f;
            
            while (networkedGM == null && elapsed < timeout)
            {
                // Find the networked instance
                var allGMs = FindObjectsOfType<NetworkGameManager>();
                foreach (var gm in allGMs)
                {
                    if (gm.Object != null && gm.Object.IsValid)
                    {
                        networkedGM = gm;
                        break;
                    }
                }
                
                if (networkedGM == null)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }
            }
            
            if (networkedGM != null)
            {
                Debug.Log($"[PlayerController] Notifying game manager that player {Object.InputAuthority} is ready");
                networkedGM.RPC_PlayerReady(Object.InputAuthority);
            }
            else
            {
                Debug.LogWarning("[PlayerController] Could not find networked game manager to notify ready state");
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(NetworkBool ready)
        {
            IsReady = ready;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestDiceRoll()
        {
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.RPC_RollDice(Object.InputAuthority);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SubmitTeamAssignmentRoll(int roll)
        {
            TeamAssignmentRoll = roll;
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.RPC_SubmitTeamRoll(Object.InputAuthority, roll);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_AttemptOptionalAction()
        {
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.RPC_AttemptOptionalAction();
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_DeclineOptionalAction()
        {
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.RPC_DeclineOptionalAction();
            }
        }

        public bool IsMyTurn()
        {
            if (NetworkGameManager.Instance == null) return false;
            return NetworkGameManager.Instance.IsLocalPlayerTurn(Object.InputAuthority);
        }

        public bool CanRoll()
        {
            if (NetworkGameManager.Instance == null) return false;
            
            var state = NetworkGameManager.Instance.CurrentState;
            if (state == GameState.RollForTeamAssignment) return true;
            if (state == GameState.DefenseTurn || state == GameState.OffenseTurn)
            {
                return IsMyTurn();
            }
            if (state == GameState.OptionalAction)
            {
                // Check if this player controls the optional action
                var actionType = NetworkGameManager.Instance.AvailableOptionalAction;
                bool isOffensiveAction = actionType == OptionalActionType.StolenBase || 
                                        actionType == OptionalActionType.TagUp;
                bool isTopOfInning = NetworkGameManager.Instance.IsTopOfInning;
                
                if (isOffensiveAction)
                {
                    // Offensive player decides
                    return isTopOfInning ? (AssignedTeam == TeamType.Away) : (AssignedTeam == TeamType.Home);
                }
                else
                {
                    // Defensive player decides (double play)
                    return isTopOfInning ? (AssignedTeam == TeamType.Home) : (AssignedTeam == TeamType.Away);
                }
            }
            return false;
        }
    }
}
