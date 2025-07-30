using System.Collections.Generic;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    private readonly List<GameMessage> messages = new()
    {
        GameMessages.Tutorial.Attack,
        GameMessages.Tutorial.Interact,
        GameMessages.Tutorial.SaveRobots,
        GameMessages.Tutorial.Badges,
        GameMessages.Tutorial.NextLevel
    };

    private int currentIndex = 0;
    private HashSet<RoomManager> visitedRooms = new();
    private readonly List<RoomManager> subscribedRooms = new();

    private void Start()
    {
        if (RunProgressManager.Instance != null && RunProgressManager.Instance.CurrentLevelIndex == 1)
        {
            var rooms = FindObjectsByType<RoomManager>(FindObjectsSortMode.None);
            foreach (var room in rooms)
            {
                room.PlayerEntered += HandlePlayerEnteredRoom;
                subscribedRooms.Add(room);
            }
        }
        else
        {
            enabled = false;
        }
    }


    private void HandlePlayerEnteredRoom(RoomManager room)
    {
        if (currentIndex < messages.Count && MessageService.Instance.IsNotDisplaying)
        {
            MessageService.Instance?.ShowMessage(messages[currentIndex]);
            currentIndex++;
        }
    }

    private void OnDestroy()
    {
        foreach (var room in subscribedRooms)
        {
            if (room != null)
            {
                room.PlayerEntered -= HandlePlayerEnteredRoom;
            }
        }
    }
}
