using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class TextSizeUniformizer : UIBehaviour
{
    [Header("Text làm chuẩn (Các text khác sẽ copy size của text này)")]
    public TextMeshProUGUI referenceText;

    [Header("Danh sách Text sẽ đồng bộ theo chuẩn")]
    public List<TextMeshProUGUI> textObjects = new List<TextMeshProUGUI>();
    
    public float minFontSize = 8f;
    public float maxFontSize = 150f; 

    private Coroutine _alignCoroutine;

    protected override void Start()
    {
        base.Start();
        MarkDirty();
    }

    // Tự động được gọi khi Screen thay đổi độ phân giải hoặc RectTransform này thay đổi kích thước
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (gameObject.activeInHierarchy)
        {
            MarkDirty();
        }
    }

    public void MarkDirty()
    {
        if (!IsActive()) return;

        if (_alignCoroutine != null)
        {
            StopCoroutine(_alignCoroutine);
        }
        _alignCoroutine = StartCoroutine(AlignRoutine());
    }

    IEnumerator AlignRoutine()
    {
        if (referenceText == null) yield break;

        // Bước 1: Cho phép text làm chuẩn tự động co giãn trong khung của nó
        referenceText.enableAutoSizing = true;
        referenceText.fontSizeMin = minFontSize;
        referenceText.fontSizeMax = maxFontSize;

        // Bước 2: Nhường quyền cho Unity update Layout/Anchor để RectTransform có kích thước thật
        yield return new WaitForEndOfFrame();

        // Bước 3: Ép Text chuẩn cập nhật để lấy ra text size (best fit) hiện tại
        referenceText.ForceMeshUpdate();
        float targetSize = referenceText.fontSize;

        if (textObjects == null || textObjects.Count == 0) yield break;

        // Bước 4: Khóa AutoSize của các Text khác và gán cứng giá trị vào danh sách Text
        foreach (var text in textObjects)
        {
            if (text == null || text == referenceText) continue;

            text.enableAutoSizing = false;
            text.fontSize = targetSize;
        }

        _alignCoroutine = null;
    }
}