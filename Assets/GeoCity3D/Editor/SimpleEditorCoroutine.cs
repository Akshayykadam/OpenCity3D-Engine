using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GeoCity3D.Editor
{
    public class SimpleEditorCoroutine
    {
        private readonly Stack<IEnumerator> _executionStack = new Stack<IEnumerator>();
        
        private SimpleEditorCoroutine(IEnumerator routine)
        {
            _executionStack.Push(routine);
            EditorApplication.update += Update;
        }
        
        private void Update()
        {
            if (_executionStack.Count == 0)
            {
                Stop();
                return;
            }

            IEnumerator top = _executionStack.Peek();

            // Check if we are waiting for an async operation
            if (top.Current is AsyncOperation op && !op.isDone)
            {
                return; 
            }
            
            // Advance execution
            if (!top.MoveNext())
            {
                // Current coroutine finished
                _executionStack.Pop();
                return;
            }
            
            // Handle nested coroutine
            if (top.Current is IEnumerator nested)
            {
                _executionStack.Push(nested);
            }
        }
        
        private void Stop()
        {
            EditorApplication.update -= Update;
        }

        public static void Start(IEnumerator routine)
        {
            new SimpleEditorCoroutine(routine);
        }
    }
}
