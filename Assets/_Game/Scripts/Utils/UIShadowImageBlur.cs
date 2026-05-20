using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class UIShadowImageBlur : MonoBehaviour
{
    [Header("Shadow Settings")]
    [SerializeField] private Vector2 offset = new Vector2(8f, -8f);
    [SerializeField, Range(0f, 10f)] private float blur = 2f;
    [SerializeField] private Color color = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private Vector2 scale = Vector2.one;

    [Header("Hierarchy")]
    [SerializeField] private int siblingOffset = -1;

    [Header("Transform")]
    [SerializeField] private bool followRotation = true;

    private const string ShadowName = "_Shadow (Auto)";
    private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");

    private Image sourceImage;
    private Image shadowImage;
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
        if (sourceImage == null)
        {
            sourceImage = GetComponent<Image>();
        }

        if (shadowImage == null)
        {
            Transform parent = transform.parent;
            Transform existing = parent != null ? parent.Find(ShadowName) : null;
            if (existing == null)
            {
                GameObject shadowObject = new GameObject(ShadowName, typeof(RectTransform));
                if (parent != null)
                {
                    shadowObject.transform.SetParent(parent, false);
                }
                existing = shadowObject.transform;
            }

            shadowImage = existing.GetComponent<Image>();
            if (shadowImage == null)
            {
                shadowImage = existing.gameObject.AddComponent<Image>();
            }

            shadowImage.raycastTarget = false;
        }

        EnsureMaterial();
    }

    private void EnsureMaterial()
    {
        if (shadowImage == null || shadowMaterialInstance != null)
        {
            return;
        }

        Shader shader = Shader.Find("Custom/UIShadowBlur");
        if (shader == null)
        {
            Debug.LogWarning("UIShadowImageBlur: Missing shader 'Custom/UIShadowBlur'.");
            return;
        }

        shadowMaterialInstance = new Material(shader)
        {
            name = "UIShadowBlur (Instance)",
            hideFlags = HideFlags.HideAndDontSave
        };

        shadowImage.material = shadowMaterialInstance;
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
        if (sourceImage == null || shadowImage == null)
        {
            return;
        }

        shadowImage.enabled = sourceImage.enabled;
        shadowImage.sprite = sourceImage.sprite;
        shadowImage.type = sourceImage.type;
        shadowImage.preserveAspect = sourceImage.preserveAspect;
        shadowImage.fillCenter = sourceImage.fillCenter;
        shadowImage.fillMethod = sourceImage.fillMethod;
        shadowImage.fillAmount = sourceImage.fillAmount;
        shadowImage.fillClockwise = sourceImage.fillClockwise;
        shadowImage.fillOrigin = sourceImage.fillOrigin;
        shadowImage.maskable = sourceImage.maskable;
        shadowImage.color = color;

        RectTransform sourceRect = sourceImage.rectTransform;
        RectTransform shadowRect = shadowImage.rectTransform;
        shadowRect.anchorMin = sourceRect.anchorMin;
        shadowRect.anchorMax = sourceRect.anchorMax;
        shadowRect.pivot = sourceRect.pivot;
        shadowRect.sizeDelta = sourceRect.sizeDelta;
        shadowRect.anchoredPosition = sourceRect.anchoredPosition + offset;
        shadowRect.localScale = new Vector3(scale.x, scale.y, 1f);

        if (followRotation)
        {
            shadowRect.localRotation = sourceRect.localRotation;
        }
        else
        {
            shadowRect.localRotation = Quaternion.identity;
        }

        if (shadowRect.parent == sourceRect.parent)
        {
            int sourceIndex = sourceRect.GetSiblingIndex();
            int desiredIndex = Mathf.Max(0, sourceIndex + siblingOffset);
            if (shadowRect.GetSiblingIndex() != desiredIndex)
            {
                shadowRect.SetSiblingIndex(desiredIndex);
            }
        }

        if (shadowMaterialInstance != null)
        {
            shadowMaterialInstance.SetFloat(BlurSizeId, blur);
        }
    }
}
