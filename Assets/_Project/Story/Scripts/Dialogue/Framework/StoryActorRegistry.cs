using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StoryActorRegistry : MonoBehaviour
{
    [Serializable]
    public sealed class ActorBinding
    {
        public string id = "actor";
        public Transform actor;
        public Transform dialogueAnchor;
        public string fallbackSceneObjectName = string.Empty;
    }

    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private Transform player;
    [SerializeField] private List<ActorBinding> actors = new List<ActorBinding>();

    public PlayerInteractor2D ResolvePlayerInteractor()
    {
        if (playerInteractor == null)
        {
            playerInteractor = FindObjectOfType<PlayerInteractor2D>(includeInactive: true);
        }

        return playerInteractor;
    }

    public Transform ResolvePlayer()
    {
        if (player != null)
        {
            return player;
        }

        PlayerInteractor2D resolvedInteractor = ResolvePlayerInteractor();
        if (resolvedInteractor != null)
        {
            player = resolvedInteractor.transform;
            return player;
        }

        player = FindSceneTransform("Player");
        return player;
    }

    public Transform ResolveActor(string id, string fallbackSceneObjectName = null)
    {
        ActorBinding binding = FindBinding(id);
        if (binding != null)
        {
            if (binding.actor == null)
            {
                string bindingFallback = !string.IsNullOrWhiteSpace(binding.fallbackSceneObjectName)
                    ? binding.fallbackSceneObjectName
                    : fallbackSceneObjectName;
                binding.actor = FindSceneTransform(bindingFallback);
            }

            if (binding.actor != null)
            {
                return binding.actor;
            }
        }

        if (string.Equals(id, "player", StringComparison.OrdinalIgnoreCase))
        {
            return ResolvePlayer();
        }

        return FindSceneTransform(fallbackSceneObjectName);
    }

    public Transform ResolveDialogueAnchor(string id, Transform fallbackActor = null, string fallbackSceneObjectName = null)
    {
        ActorBinding binding = FindBinding(id);
        if (binding != null)
        {
            if (binding.dialogueAnchor != null)
            {
                return binding.dialogueAnchor;
            }

            Transform actor = binding.actor != null
                ? binding.actor
                : ResolveActor(id, fallbackSceneObjectName);
            if (actor != null)
            {
                return actor;
            }
        }

        return fallbackActor != null
            ? fallbackActor
            : ResolveActor(id, fallbackSceneObjectName);
    }

    private ActorBinding FindBinding(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        for (int i = 0; i < actors.Count; i++)
        {
            ActorBinding binding = actors[i];
            if (binding == null)
            {
                continue;
            }

            if (string.Equals(binding.id, id, StringComparison.OrdinalIgnoreCase))
            {
                return binding;
            }
        }

        return null;
    }

    private static Transform FindSceneTransform(string sceneObjectName)
    {
        if (string.IsNullOrWhiteSpace(sceneObjectName))
        {
            return null;
        }

        GameObject sceneObject = GameObject.Find(sceneObjectName);
        return sceneObject != null ? sceneObject.transform : null;
    }
}
