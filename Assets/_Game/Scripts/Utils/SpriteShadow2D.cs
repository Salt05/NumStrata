using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteShadow2D : MonoBehaviour
{
    [Header("Shadow Settings")]
    [SerializeField] private Vector2 offset = new Vector2(0.1f, -0.1f);
    [SerializeField, Range(0f, 10f)] private float blur = 2f;
    [SerializeField] private Color color = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private float scale = 1f;

    [Header("Sorting")]
    [SerializeField] private bool matchSortingLayer = true;
    [SerializeField] private bool matchSortingOrder = true;
    [SerializeField] private int sortingOrderOffset = -1;

    [Header("Transform")]
    [SerializeField] private bool followRotation = true;

    private const string ShadowChildName = "_Shadow (Auto)";
    private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer shadowRenderer;
    private Material shadowMaterialInstance;

    private void OnEnable()
    {
        EnsureShadow();
        SyncShadow();
    }

    private void OnDisable()
    {
        CleanupMaterial();
    }

    private void OnValidate()
    {
        EnsureShadow();
        SyncShadow();
    }

    private void LateUpdate()
    {
        SyncShadow();
    }

    private void EnsureShadow()
    {
        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }

        if (shadowRenderer == null)
        {
            Transform child = transform.Find(ShadowChildName);
            if (child == null)
            {
                GameObject childObject = new GameObject(ShadowChildName);
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }

            shadowRenderer = child.GetComponent<SpriteRenderer>();
            if (shadowRenderer == null)
            {
                shadowRenderer = child.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        EnsureMaterial();
    }

    private void EnsureMaterial()
    {
        if (shadowRenderer == null || shadowMaterialInstance != null)
        {
            return;
        }

        Shader shader = Shader.Find("Custom/SpriteShadowBlur");
        if (shader == null)
        {
            Debug.LogWarning("SpriteShadow2D: Missing shader 'Custom/SpriteShadowBlur'.");
            return;
        }

        shadowMaterialInstance = new Material(shader)
        {
            name = "ShadowBlur (Instance)",
            hideFlags = HideFlags.HideAndDontSave
        };

        shadowRenderer.sharedMaterial = shadowMaterialInstance;
    }

    private void CleanupMaterial()
    {
        if (shadowMaterialInstance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(shadowMaterialInstance);
        }
        else
        {
            DestroyImmediate(shadowMaterialInstance);
        }

        shadowMaterialInstance = null;
    }

    private void SyncShadow()
    {
        if (sourceRenderer == null || shadowRenderer == null)
        {
            return;
        }

        shadowRenderer.enabled = sourceRenderer.enabled;
        shadowRenderer.sprite = sourceRenderer.sprite;
        shadowRenderer.flipX = sourceRenderer.flipX;
        shadowRenderer.flipY = sourceRenderer.flipY;
        shadowRenderer.drawMode = sourceRenderer.drawMode;
        shadowRenderer.size = sourceRenderer.size;
        shadowRenderer.maskInteraction = sourceRenderer.maskInteraction;

        if (matchSortingLayer)
        {
            shadowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        }

        if (matchSortingOrder)
        {
            shadowRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
        }

        shadowRenderer.color = color;

        Transform shadowTransform = shadowRenderer.transform;
        shadowTransform.localPosition = new Vector3(offset.x, offset.y, 0f);
        shadowTransform.localScale = Vector3.one * Mathf.Max(0.0001f, scale);

        if (followRotation)
        {
            shadowTransform.localRotation = Quaternion.identity;
        }
        else
        {
            shadowTransform.localRotation = Quaternion.Inverse(transform.rotation);
        }

        if (shadowMaterialInstance != null)
        {
            shadowMaterialInstance.SetFloat(BlurSizeId, blur);
        }
    }
}
