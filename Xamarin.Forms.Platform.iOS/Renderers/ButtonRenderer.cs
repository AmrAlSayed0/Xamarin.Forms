﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Foundation;
using UIKit;
using Xamarin.Forms.Internals;
using Xamarin.Forms.PlatformConfiguration.iOSSpecific;
using Specifics = Xamarin.Forms.PlatformConfiguration.iOSSpecific;
using SizeF = CoreGraphics.CGSize;
using PreserveAttribute = Foundation.PreserveAttribute;
using CoreGraphics;

namespace Xamarin.Forms.Platform.iOS
{
	public class ButtonRenderer : ViewRenderer<Button, UIButton>, IImageVisualElementRenderer, IButtonLayoutRenderer
	{
		bool _isDisposed;
		SizeF _previousSize;
		UIColor _buttonTextColorDefaultDisabled;
		UIColor _buttonTextColorDefaultHighlighted;
		UIColor _buttonTextColorDefaultNormal;
		bool _useLegacyColorManagement;

		ButtonLayoutManager _buttonLayoutManager;

		// This looks like it should be a const under iOS Classic,
		// but that doesn't work under iOS 
		// ReSharper disable once BuiltInTypeReferenceStyle
		// Under iOS Classic Resharper wants to suggest this use the built-in type ref
		// but under iOS that suggestion won't work
		readonly nfloat _minimumButtonHeight = 44; // Apple docs 

		static readonly UIControlState[] s_controlStates = { UIControlState.Normal, UIControlState.Highlighted, UIControlState.Disabled };

		public bool IsDisposed => _isDisposed;

		IImageVisualElementRenderer IButtonLayoutRenderer.ImageVisualElementRenderer => this;
		nfloat IButtonLayoutRenderer.MinimumHeight => _minimumButtonHeight;

		[Preserve(Conditional = true)]
		public ButtonRenderer()
		{
			BorderElementManager.Init(this);

			_buttonLayoutManager = new ButtonLayoutManager(this);
		}

		public override SizeF SizeThatFits(SizeF size)
		{
			var measured = base.SizeThatFits(size);
			return _buttonLayoutManager?.SizeThatFits(size, measured) ?? measured;
		}

		protected override void Dispose(bool disposing)
		{
			if (_isDisposed)
				return;

			_isDisposed = true;

			if (disposing)
			{
				if (Control != null)
				{
					Control.TouchUpInside -= OnButtonTouchUpInside;
					Control.TouchDown -= OnButtonTouchDown;
					BorderElementManager.Dispose(this);
					_buttonLayoutManager?.Dispose();
					_buttonLayoutManager = null;
				}
			}

			base.Dispose(disposing);
		}

		public override void Draw(CGRect rect)
		{
			base.Draw(rect);

			_previousSize = Bounds.Size;
		}

		public override void LayoutSubviews()
		{
			if (_previousSize != Bounds.Size)
			{
				SetBackground(Element.Background);
				SetNeedsDisplay();
			}

			base.LayoutSubviews();
		}

		protected override void OnElementChanged(ElementChangedEventArgs<Button> e)
		{
			base.OnElementChanged(e);

			if (e.NewElement != null)
			{
				if (Control == null)
				{
					SetNativeControl(CreateNativeControl());

					Debug.Assert(Control != null, "Control != null");

					SetControlPropertiesFromProxy();

					_useLegacyColorManagement = e.NewElement.UseLegacyColorManagement();

					_buttonTextColorDefaultNormal = Control.TitleColor(UIControlState.Normal);
					_buttonTextColorDefaultHighlighted = Control.TitleColor(UIControlState.Highlighted);
					_buttonTextColorDefaultDisabled = Control.TitleColor(UIControlState.Disabled);

					Control.TouchUpInside += OnButtonTouchUpInside;
					Control.TouchDown += OnButtonTouchDown;
				}

				UpdateFont();
				UpdateTextColor();
				_buttonLayoutManager?.Update();
			}
		}

		protected override UIButton CreateNativeControl()
		{
			return new UIButton(UIButtonType.System);
		}

		protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			base.OnElementPropertyChanged(sender, e);

			if (e.PropertyName == Button.TextColorProperty.PropertyName)
				UpdateTextColor();
			else if (e.PropertyName == Button.FontProperty.PropertyName)
				UpdateFont();
		}

		protected override void SetAccessibilityLabel()
		{
			// If we have not specified an AccessibilityLabel and the AccessibilityLabel is currently bound to the Title,
			// exit this method so we don't set the AccessibilityLabel value and break the binding.
			// This may pose a problem for users who want to explicitly set the AccessibilityLabel to null, but this
			// will prevent us from inadvertently breaking UI Tests that are using Query.Marked to get the dynamic Title 
			// of the Button.

			var elemValue = (string)Element?.GetValue(AutomationProperties.NameProperty);
			if (string.IsNullOrWhiteSpace(elemValue) && Control?.AccessibilityLabel == Control?.Title(UIControlState.Normal))
				return;

			base.SetAccessibilityLabel();
		}

		protected override void SetBackground(Brush brush)
		{
			if (Control == null)
				return;

			UIColor backgroundColor = Element.BackgroundColor == Color.Default ? null : Element.BackgroundColor.ToUIColor();

			if (!Brush.IsNullOrEmpty(brush))
			{
				if (brush is SolidColorBrush solidColorBrush)
					backgroundColor = solidColorBrush.Color.ToUIColor();
				else
				{
					var backgroundImage = this.GetBackgroundImage(brush);
					if (backgroundImage != null)
						backgroundColor = UIColor.FromPatternImage(backgroundImage);
				}
			}

			Control.BackgroundColor = backgroundColor;
		}

		void SetControlPropertiesFromProxy()
		{
			foreach (UIControlState uiControlState in s_controlStates)
			{
				Control.SetTitleColor(UIButton.Appearance.TitleColor(uiControlState), uiControlState); // if new values are null, old values are preserved.
				Control.SetTitleShadowColor(UIButton.Appearance.TitleShadowColor(uiControlState), uiControlState);
				Control.SetBackgroundImage(UIButton.Appearance.BackgroundImageForState(uiControlState), uiControlState);
			}
		}

		void OnButtonTouchUpInside(object sender, EventArgs eventArgs)
		{
			ButtonElementManager.OnButtonTouchUpInside(this.Element);
		}

		void OnButtonTouchDown(object sender, EventArgs eventArgs)
		{
			ButtonElementManager.OnButtonTouchDown(this.Element);
		}

		void UpdateFont()
		{
			Control.TitleLabel.Font = Element.ToUIFont();
		}

		public void SetImage(UIImage image) => _buttonLayoutManager.SetImage(image);

		public UIImageView GetImage() => Control?.ImageView;

		void UpdateTextColor()
		{
			if (Element.TextColor == Color.Default)
			{
				Control.SetTitleColor(_buttonTextColorDefaultNormal, UIControlState.Normal);
				Control.SetTitleColor(_buttonTextColorDefaultHighlighted, UIControlState.Highlighted);
				Control.SetTitleColor(_buttonTextColorDefaultDisabled, UIControlState.Disabled);
			}
			else
			{
				var color = Element.TextColor.ToUIColor();

				Control.SetTitleColor(color, UIControlState.Normal);
				Control.SetTitleColor(color, UIControlState.Highlighted);
				Control.SetTitleColor(_useLegacyColorManagement ? _buttonTextColorDefaultDisabled : color, UIControlState.Disabled);

				Control.TintColor = color;
			}
		}
	}
}
