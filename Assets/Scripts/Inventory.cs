// Inventory.cs
using UnityEngine;
using System.Collections.Generic;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }
    private HashSet<string> items = new HashSet<string>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool Has(string id) => items.Contains(id);
    public bool Add(string id) => items.Add(id);
    public bool Remove(string id) => items.Remove(id);
}
