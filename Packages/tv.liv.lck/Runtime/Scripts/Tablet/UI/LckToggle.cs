using Liv.Lck.Tablet;
using Liv.Lck.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

namespace Liv.Lck.UI
{
    public class LckToggle : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Settings")]
        [SerializeField]
        private string _name;

        [SerializeField]
        private Sprite _icon;

        [SerializeField]
        private Sprite _iconOn;

        private Tuple<Sprite, Sprite> _defaultIcons;

        [SerializeField]
        private LckButtonColors _colors;

        [SerializeField]
        private LckButtonColors _colorsOn;

        private Tuple<LckButtonColors, LckButtonColors> _defaultColors;

        [SerializeField]
        private Vector3 _togglePressedPosition = new Vector3(0, 0, 40f);

        [Header("Toggle Group Settings")]
        [SerializeField]
        private bool _stayPressedDownWhenToggled = false;    

        [Header("References")]
        [SerializeField]
        private TMPro.TextMeshProUGUI _labelText;

        [SerializeField]
        private Renderer _renderer;

        [SerializeField]
        private RectTransform _visuals;

        [SerializeField]
        private Image _iconImage;

        [SerializeField]
        private Toggle _toggle;

        [Header("Audio")]
        [SerializeField]
        private LckDiscreetAudioController _audioController;

        private bool _collided;
        private GameObject _clickedObject;
        private MaterialPropertyBlock _propertyBlock;
        private int _colorId;
        public bool IsDisabled { get; private set; } = false;

        private void Awake()
        {
            _defaultColors = new Tuple<LckButtonColors, LckButtonColors>(_colors, _colorsOn);
            _defaultIcons = new Tuple<Sprite, Sprite>(_icon, _iconOn);
        }

        private void Start()
        {
            ValidateMeshColors();
            _toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        public void OnToggleValueChanged(bool value)
        {
            ValidateIcon();
            ValidateColors();
            ValidateMeshColors();

            if (_toggle.group != null && value == false)
                _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
        }

        #region Using ray and poke
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsDisabled == true) return;

            SetMeshColor(_toggle.isOn && _colorsOn ? _colorsOn.HighlightedColor : _colors.HighlightedColor);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.HoverSound);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsDisabled == true) return;

            _visuals.anchoredPosition3D = _togglePressedPosition;
            SetMeshColor(_toggle.isOn && _colorsOn ? _colorsOn.PressedColor : _colors.PressedColor);
            _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickDown);

            if (eventData != null)
                _clickedObject = eventData.pointerEnter;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (IsDisabled == true) return;

            if (_toggle.group == null || _stayPressedDownWhenToggled == false)
            {
                _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
                _audioController.PlayDiscreetAudioClip(LckDiscreetAudioController.AudioClip.ClickUp);
            }

            if (eventData != null)
            {
                // If you release away from the button on the way up, still send an OnClick event
                if (_clickedObject != eventData.pointerEnter)
                {
                    _toggle.OnPointerClick(eventData);
                    SetMeshColor(_toggle.isOn && _colorsOn ? _colorsOn.NormalColor : _colors.NormalColor);
                    return;
                }
            }
            
            //NOTE: toggle isOn value hasn't changed at this point, so using the opposite works 
            SetMeshColor(!_toggle.isOn && _colorsOn ? _colorsOn.HighlightedColor : _colors.HighlightedColor);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsDisabled == true) return;

            SetMeshColor(_toggle.isOn && _colorsOn ? _colorsOn.NormalColor : _colors.NormalColor);

            // when using poke and releasing away from button need to make sure to reset position
            if (_toggle.group == null || _stayPressedDownWhenToggled == false)
            {
                _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
            }
        }
        #endregion

        #region Using colliders
        private void OnTriggerEnter(Collider other)
        {
            if (IsDisabled == true) return;

            if (other.gameObject.CompareTag(LckSettings.Instance.TriggerEnterTag) 
                && IsValidTap(other.ClosestPoint(transform.position)) 
                && LCKCameraController.ColliderButtonsInUse == false)
            {
                LCKCameraController.ColliderButtonsInUse = true;
                _collided = true;

                OnPointerDown(null);

                // this will manually activate the toggle and send an event
                _toggle.isOn = !_toggle.isOn;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsDisabled == true) return;

            if (_collided == true)
            {
                OnPointerUp(null);
                OnPointerExit(null);
                _collided = false;
                LCKCameraController.ColliderButtonsInUse = false;
            }
        }

        private bool IsValidTap(Vector3 tapPosition)
        {
            Vector3 direction = tapPosition - transform.position;
            float angle = Vector3.Angle(-transform.forward, direction);
            return angle < 90;
        }
        #endregion

        private void OnApplicationFocus(bool focus)
        {
            if (focus == true)
            {
                _collided = false;

                if (_stayPressedDownWhenToggled == true && _toggle.isOn)
                {
                    _visuals.anchoredPosition3D = _togglePressedPosition;
                }
                else
                {
                    _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
                }

                // reset the button visuals to default
                SetMeshColor(_toggle.isOn && _colorsOn ? _colorsOn.NormalColor : _colors.NormalColor);
            }
        }

        public void SetDisabledState(bool usePressedPosition = false)
        {
            IsDisabled = true;
            
            if (usePressedPosition == true)
            {
                _visuals.anchoredPosition3D = _togglePressedPosition;
            }
            else
            {
                _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
            }

            _toggle.enabled = false;
        }

        public void RestoreToggleState()
        {
            IsDisabled = false;
            _visuals.anchoredPosition3D = new Vector3(0, 0, 0f);
            _toggle.enabled = true;
        }

        public void SetToggleVisualsOff()
        {
            _toggle.SetIsOnWithoutNotify(false);
            SetMeshColor(_colors.NormalColor);
            ValidateIcon();
        }

        public void SetToggleVisualsOn()
        {
            _toggle.SetIsOnWithoutNotify(true);
            SetMeshColor(_toggle.isOn && _colorsOn ? _colorsOn.NormalColor : _colors.NormalColor);
            ValidateIcon();
        }

        public void SetCustomColors(LckButtonColors colors, LckButtonColors colorsOn)
        {
            _colors = colors;
            _colorsOn = colorsOn;
            ValidateMeshColors();
        }

        public void RestoreDefaultColors()
        {
            _colors = _defaultColors.Item1;
            _colorsOn = _defaultColors.Item2;
            ValidateMeshColors();
        }

        public void SetCustomIcons(Sprite icon, Sprite iconOn)
        {
            _icon = icon;
            _iconOn = iconOn;
            ValidateIcon();
        }

        public void RestoreDefaultIcons()
        {
            _icon = _defaultIcons.Item1;
            _iconOn = _defaultIcons.Item2;
            ValidateIcon();
        }

        private void ValidateColors()
        {
            if (_colors)
            {
                if (_toggle.isOn && _colorsOn)
                {
                    var colors = _toggle.colors;
                    colors.normalColor = _colorsOn.NormalColor;
                    colors.highlightedColor = _colorsOn.HighlightedColor;
                    colors.pressedColor = _colorsOn.PressedColor;
                    colors.selectedColor = _colorsOn.SelectedColor;
                    colors.disabledColor = _colorsOn.DisabledColor;

                    if (_toggle.colors != colors)
                    {
                        _toggle.colors = colors;
                    }
                }
                else
                {
                    var colors = _toggle.colors;
                    colors.normalColor = _colors.NormalColor;
                    colors.highlightedColor = _colors.HighlightedColor;
                    colors.pressedColor = _colors.PressedColor;
                    colors.selectedColor = _colors.SelectedColor;
                    colors.disabledColor = _colors.DisabledColor;

                    if (_toggle.colors != colors)
                    {
                        _toggle.colors = colors;
                    }
                }
            }
        }

        private void ValidateIcon()
        {
            if (_iconImage && _icon)
            {
                if (_toggle.isOn && _iconOn != null)
                {
                    _iconImage.sprite = _iconOn;
                }
                else
                {
                    _iconImage.sprite = _icon;
                }

                if (!_iconImage.gameObject.activeSelf)
                {
                    _iconImage.gameObject.SetActive(true);
                }

                if (_labelText && _labelText.gameObject.activeSelf)
                {
                    _labelText.gameObject.SetActive(false);
                }
            }
            else
            {
                if (_iconImage && _iconImage.gameObject.activeSelf)
                {
                    _iconImage.gameObject.SetActive(false);
                }

                if (_labelText && !_labelText.gameObject.activeSelf)
                {
                    _labelText.gameObject.SetActive(true);
                }
            }
        }

        private void ValidateMeshColors()
        {
            if (!_renderer) return;

            _propertyBlock ??= new MaterialPropertyBlock();
            
            if (_colorId == 0)
                _colorId = Shader.PropertyToID("_Color");
            
            SetMeshColor(_toggle.isOn && _colorsOn ? _colorsOn.NormalColor : _colors.NormalColor);
        }

        private void OnValidate()
        {
            if (_labelText)
                _labelText.text = _name;

            if (_toggle.group != null && _stayPressedDownWhenToggled == true)
                if (_toggle.isOn)
                    _visuals.anchoredPosition3D = new Vector3(0, 0, 40f);

            ValidateIcon();
            ValidateColors();
            ValidateMeshColors();
        }
        
        private void SetMeshColor(Color color)
        {
            _propertyBlock ??= new MaterialPropertyBlock();

            _propertyBlock.SetColor(_colorId, color);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
