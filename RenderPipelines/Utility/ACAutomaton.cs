using System;
using System.Collections.Generic;

namespace RenderPipelines.Utility;

public class ACAutomaton
{
    public delegate void MatchCallback(int start, int end);

    private class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = new Dictionary<char, TrieNode>();
        public TrieNode Fail { get; set; }
        public List<(MatchCallback Callback, int Length)> Callbacks { get; } = new List<(MatchCallback, int)>();
    }

    private readonly TrieNode root = new TrieNode();
    private bool isBuilt = false;

    public void AddMatch(string match, MatchCallback callback)
    {
        if (string.IsNullOrEmpty(match))
            throw new ArgumentException("Match string cannot be null or empty.");
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        TrieNode current = root;
        foreach (char c in match)
        {
            if (!current.Children.ContainsKey(c))
            {
                current.Children[c] = new TrieNode();
            }
            current = current.Children[c];
        }
        current.Callbacks.Add((callback, match.Length));
        isBuilt = false; // Adding new patterns invalidates the fail pointers
    }

    public void BuildFail()
    {
        Queue<TrieNode> queue = new Queue<TrieNode>();
        root.Fail = null;

        // Initialize root's children
        foreach (var child in root.Children.Values)
        {
            child.Fail = root;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            TrieNode current = queue.Dequeue();

            foreach (var kvp in current.Children)
            {
                char c = kvp.Key;
                TrieNode child = kvp.Value;

                TrieNode failNode = current.Fail;
                while (failNode != null && !failNode.Children.ContainsKey(c))
                {
                    failNode = failNode.Fail;
                }

                child.Fail = failNode != null ? failNode.Children[c] : root;
                queue.Enqueue(child);
            }
        }

        isBuilt = true;
    }

    public void Search(string text)
    {
        if (!isBuilt)
            throw new InvalidOperationException("ACAutomaton must be built with BuildFail() before searching.");

        TrieNode current = root;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Follow fail pointers until we find a node with child 'c' or reach root
            while (current != root && !current.Children.ContainsKey(c))
            {
                current = current.Fail;
            }

            // Move to the child if exists, otherwise stays at root
            if (current.Children.TryGetValue(c, out TrieNode nextNode))
            {
                current = nextNode;
            }

            // Check all fail paths for matches
            TrieNode temp = current;
            while (temp != root)
            {
                foreach (var (callback, length) in temp.Callbacks)
                {
                    int start = i - length + 1;
                    if (start >= 0)
                    {
                        callback(start, i);
                    }
                }
                temp = temp.Fail;
            }
        }
    }
}

