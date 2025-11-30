using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class BeatSaberMapParser : MonoBehaviour
{
    [System.Serializable]
    public class BeatSaberMap
    {
        public string _version;
        public List<Note> _notes;
        public List<Obstacle> _obstacles;
        public List<Event> _events;
    }

    [System.Serializable]
    public class Note
    {
        public float _time;
        public int _lineIndex;
        public int _lineLayer;
        public int _type;
        public int _cutDirection;
    }

    [System.Serializable]
    public class Obstacle
    {
        public float _time;
        public int _lineIndex;
        public int _type;
        public float _duration;
        public int _width;
    }

    [System.Serializable]
    public class Event
    {
        public float _time;
        public int _type;
        public int _value;
    }

    public static BeatSaberMap ParseMap(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogError("Map file not found: " + jsonPath);
            return null;
        }

        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            BeatSaberMap map = JsonUtility.FromJson<BeatSaberMap>(jsonContent);
            Debug.Log($"Parsed map: {map._notes?.Count} notes, {map._obstacles?.Count} obstacles, {map._events?.Count} events");
            return map;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error parsing map: " + e.Message);
            return null;
        }
    }
}