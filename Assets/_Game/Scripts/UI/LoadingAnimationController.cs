using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NumStrata.UI
{
    public class LoadingAnimationController : MonoBehaviour
    {
        [Header("Tile Prefab")]
        [Tooltip("The Tile Prefab containing Image and Animator components.")]
        public GameObject tilePrefab;

        [Header("Sprite Assets")]
        [Tooltip("Assign sprites for digits 0 to 9 in matching index positions.")]
        public Sprite[] digitSprites = new Sprite[10];
        
        public Sprite plusSprite;
        public Sprite minusSprite;
        public Sprite multiplySprite;
        public Sprite divideSprite;
        public Sprite equalSprite;

        [Header("UI Slot Parents")]
        [Tooltip("Assign the 6 parent Slot transforms/GameObjects in order from left to right.")]
        public Transform[] slotParents = new Transform[6];

        [Header("Animation Settings")]
        [Tooltip("Names of the entry (fly-in) states in the Animator Controller.")]
        public string[] flyInStateNames = { "FlyIn_1", "FlyIn_2", "FlyIn_3" };
        
        [Tooltip("Name of the exit (shrink-out) state in the Animator Controller.")]
        public string shrinkStateName = "Shrink_Out";
        
        [Tooltip("Delay in seconds between animating consecutive tiles.")]
        public float delayBetweenTiles = 0.15f;
        
        [Tooltip("Delay in seconds after all tiles are shown before starting the shrink animation.")]
        public float delayBeforeShrink = 0.5f;
        
        [Tooltip("Duration of the shrink animation (wait time before starting the next loop).")]
        public float shrinkDuration = 0.5f;

        private Coroutine loopCoroutine;
        private bool isDoneLoading = false;
        private bool isLoopRunning = false;
        private List<GameObject> spawnedTiles = new List<GameObject>();

        public bool IsLoopRunning => isLoopRunning;

        private void OnEnable()
        {
            StartLoop();
        }

        private void OnDisable()
        {
            StopLoop();
        }

        /// <summary>
        /// Starts the loading animation loop.
        /// </summary>
        public void StartLoop()
        {
            isDoneLoading = false;
            if (loopCoroutine != null)
            {
                StopCoroutine(loopCoroutine);
            }
            loopCoroutine = StartCoroutine(AnimationLoopRoutine());
        }

        /// <summary>
        /// Stops the loading animation loop.
        /// </summary>
        public void StopLoop()
        {
            if (loopCoroutine != null)
            {
                StopCoroutine(loopCoroutine);
                loopCoroutine = null;
            }
            isLoopRunning = false;
            ClearSpawnedTiles();
        }

        /// <summary>
        /// Signals that the loading process is complete.
        /// The loop will finish its current run before disabling the panel.
        /// </summary>
        public void SetDoneLoading()
        {
            isDoneLoading = true;
        }

        private IEnumerator AnimationLoopRoutine()
        {
            isLoopRunning = true;
            
            while (true)
            {
                // 1. Clear any existing spawned tiles first
                ClearSpawnedTiles();

                // 2. Generate math equation
                string equation = GenerateEquation();
                int len = Mathf.Min(equation.Length, slotParents.Length);

                List<Animator> spawnedAnimators = new List<Animator>();

                // 3. Spawn tiles and play entry animations one by one
                for (int i = 0; i < len; i++)
                {
                    Transform parent = slotParents[i];
                    if (parent == null || tilePrefab == null) continue;

                    char c = equation[i];
                    Sprite sprite = GetSpriteForChar(c);

                    // Instantiate tile as a child of the slot parent
                    GameObject tileObj = Instantiate(tilePrefab, parent);
                    tileObj.transform.localPosition = Vector3.zero;
                    tileObj.transform.localRotation = Quaternion.identity;
                    tileObj.transform.localScale = Vector3.one;
                    spawnedTiles.Add(tileObj);

                    // Setup Image component
                    Image img = tileObj.GetComponentInChildren<Image>();
                    if (img != null)
                    {
                        img.sprite = sprite;
                    }

                    // Setup Animator component
                    Animator anim = tileObj.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        spawnedAnimators.Add(anim);
                        if (flyInStateNames != null && flyInStateNames.Length > 0)
                        {
                            string stateName = flyInStateNames[Random.Range(0, flyInStateNames.Length)];
                            anim.Play(stateName, 0, 0f);
                        }
                    }

                    yield return new WaitForSecondsRealtime(delayBetweenTiles);
                }

                // Wait until the last slot completes its entrance plus a small delay
                yield return new WaitForSecondsRealtime(delayBeforeShrink);

                // 4. Shrink all active tiles
                foreach (var anim in spawnedAnimators)
                {
                    if (anim != null && !string.IsNullOrEmpty(shrinkStateName))
                    {
                        anim.Play(shrinkStateName, 0, 0f);
                    }
                }

                // Wait for shrink animation to finish
                yield return new WaitForSecondsRealtime(shrinkDuration);

                // 5. Check if loading is complete to exit the loop
                if (isDoneLoading)
                {
                    break;
                }
            }

            ClearSpawnedTiles();
            isLoopRunning = false;
            gameObject.SetActive(false);
        }

        private void ClearSpawnedTiles()
        {
            foreach (var tile in spawnedTiles)
            {
                if (tile != null)
                {
                    Destroy(tile);
                }
            }
            spawnedTiles.Clear();

            // Fallback safety cleanup to avoid UI residue
            foreach (var parent in slotParents)
            {
                if (parent != null)
                {
                    foreach (Transform child in parent)
                    {
                        if (child != null)
                        {
                            Destroy(child.gameObject);
                        }
                    }
                }
            }
        }

        private Sprite GetSpriteForChar(char c)
        {
            if (c >= '0' && c <= '9')
            {
                int digit = c - '0';
                if (digitSprites != null && digit < digitSprites.Length)
                {
                    return digitSprites[digit];
                }
            }
            else if (c == '+') return plusSprite;
            else if (c == '-' || c == '−') return minusSprite;
            else if (c == '*' || c == 'x' || c == 'X' || c == '×') return multiplySprite;
            else if (c == '/' || c == ':') return divideSprite;
            else if (c == '=') return equalSprite;

            return null;
        }

        private string GenerateEquation()
        {
            // Hỗ trợ cả 4 phép tính (+, -, x, /) nhưng đảm bảo cả 2 toán hạng bên vế trái luôn là 1 chữ số
            int opType = Random.Range(0, 4);
            int a = 0, b = 0, c = 0;
            char op = '+';

            switch (opType)
            {
                case 0: // Phép cộng (A + B = C)
                    op = '+';
                    a = Random.Range(1, 10); // 1 đến 9
                    b = Random.Range(1, 10); // 1 đến 9
                    c = a + b; // Kết quả có thể là 1 hoặc 2 chữ số
                    break;

                case 1: // Phép trừ (A - B = C)
                    op = '-';
                    a = Random.Range(1, 10); // 1 đến 9
                    b = Random.Range(1, a + 1); // 1 đến a để kết quả không âm
                    c = a - b; // Kết quả luôn là 1 chữ số
                    break;

                case 2: // Phép nhân (A * B = C)
                    op = 'x';
                    a = Random.Range(1, 10); // 1 đến 9
                    b = Random.Range(1, 10); // 1 đến 9
                    c = a * b; // Kết quả có thể là 1 hoặc 2 chữ số
                    break;

                case 3: // Phép chia (A / B = C)
                    op = '/';
                    b = Random.Range(1, 10); // Số chia là 1 chữ số (1 đến 9)
                    int maxC = Mathf.FloorToInt(9f / b);
                    c = Random.Range(1, maxC + 1); // Thương số (1 chữ số) để đảm bảo số bị chia (a) <= 9
                    a = b * c; // Số bị chia luôn là 1 chữ số
                    break;
            }

            return $"{a}{op}{b}={c}";
        }
    }
}
