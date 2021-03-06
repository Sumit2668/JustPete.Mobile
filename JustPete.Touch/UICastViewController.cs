﻿using System;
using Foundation;
using GoogleCast;
using JustPete.Touch.Infrastructure;
using UIKit;

namespace JustPete.Touch
{
	public partial class UICastViewController : UIViewController
	{
		const string CAST_APP_ID = "A487EF70";

		GCKDeviceScanner _deviceScanner;

		GCKDeviceManager _deviceManager;

		GCKFilterCriteria _filterCriteria;

		UIBarButtonItem _googleCastButton;

		GCKDevice _selectedDevice;

		MyChannel _channel;

		bool _applicationStarted;

		bool _hasJoined;

		public UICastViewController () : base ("UICastViewController", null)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			Title = "Just Pete";

			NavigationItem.RightBarButtonItems = new UIBarButtonItem[0];

			Setup ();

			CreateCastButton ();

			StartScanning ();
		}

		void Setup()
		{
			textGuess.ReturnKeyType = UIReturnKeyType.Go;
			textName.ShouldReturn += textField => {
				OnJoin ();
				return true;
			};

			buttonJoin.TouchUpInside += (sender, e) => OnJoin();

			buttonGuess.TouchUpInside += (sender, e) => {
				if (string.IsNullOrWhiteSpace(textGuess.Text))
					return;

				int val;
				if (int.TryParse(textGuess.Text.Trim(), out val))
					_channel.Guess(val);

				textGuess.Text = string.Empty;
				//textGuess.SelectedTextRange = textGuess.GetTextRange(textGuess.BeginningOfDocument, textGuess.EndOfDocument);
			};

			UpdateButtonStates ();
		}

		void OnJoin()
		{
			if (string.IsNullOrWhiteSpace(textName.Text))
				return;

			_channel.Join(textName.Text.Trim());
			_hasJoined = true;
			UpdateButtonStates();

			textGuess.BecomeFirstResponder();
		}

		void CreateCastButton ()
		{
			_googleCastButton = new UIBarButtonItem (
				UIImage.FromFile ("icon-cast-identified.png"),
				UIBarButtonItemStyle.Plain, (s, e) =>  ChooseDevice());
		}

		void UpdateButtonStates()
		{
			if (_deviceScanner != null && _deviceScanner.Devices.Length > 0)
			{
				// Show the Cast button.
				NavigationItem.RightBarButtonItems = new[] { _googleCastButton };
				if (_deviceManager != null && _deviceManager.ConnectionState == GCKConnectionState.Connected)
				{
					_googleCastButton.Image = UIImage.FromFile ("icon-cast-connected.png");
				}
				else
				{
					_googleCastButton.Image = UIImage.FromFile ("icon-cast-identified.png");
				}
			}
			else
			{
				NavigationItem.RightBarButtonItems = new UIBarButtonItem[0];
			}

			promptCast.Enabled = !_applicationStarted;

			promptJoin.Enabled = _applicationStarted && !_hasJoined;
			textName.Enabled = promptJoin.Enabled;
			//textName.Hint = _textName.Enabled ? "Name" : string.Empty;
			buttonJoin.Enabled = promptJoin.Enabled;

			promptGuess.Enabled = _applicationStarted && _hasJoined;
			textGuess.Enabled = promptGuess.Enabled;
			//textGuess.Hint = _textGuess.Enabled ? "Guess" : string.Empty;
			buttonGuess.Enabled = promptGuess.Enabled;
		}

		void StartScanning ()
		{
			_filterCriteria = GCKFilterCriteria.FromAvailableApplication (CAST_APP_ID);

			_deviceScanner = new GCKDeviceScanner { FilterCriteria = _filterCriteria };

			var deviceScannerListener = new DeviceScannerListener
			{
				OnDeviceDidComeOnline = d => UpdateButtonStates (),
				OnDeviceDidGoOffline = d => UpdateButtonStates ()
			};

			_deviceScanner.AddListener(deviceScannerListener);

			_deviceScanner.StartScan ();
		}

		void ChooseDevice()
		{
			if (_selectedDevice == null) {
				Console.WriteLine ("Choose device action sheet");

				var actionSheet = new UIActionSheet (NSBundle.MainBundle.LocalizedString ("ConnectToDevice", null));
				foreach (var device in _deviceScanner.Devices)
					actionSheet.AddButton (device.FriendlyName);

				actionSheet.AddButton (NSBundle.MainBundle.LocalizedString ("Cancel", null));
				actionSheet.CancelButtonIndex = actionSheet.ButtonCount - 1;
				actionSheet.Clicked += (sender, e) => OnDeviceSelected ((int)e.ButtonIndex);
				actionSheet.ShowInView (View);
			} else {
				Console.WriteLine ("Connected device action sheet");

				var actionSheet = new UIActionSheet (_selectedDevice.FriendlyName);
				actionSheet.AddButton (NSBundle.MainBundle.LocalizedString ("Disconnect", null));
				actionSheet.AddButton (NSBundle.MainBundle.LocalizedString ("Cancel", null));
				actionSheet.DestructiveButtonIndex = 0;
				actionSheet.CancelButtonIndex = 1;
				actionSheet.Clicked += (sender, e) => OnDisconnect();
				actionSheet.ShowInView (View);
			}
		}

		void OnDeviceSelected (int deviceIndex)
		{
			_selectedDevice = _deviceScanner.Devices [deviceIndex];
			Console.WriteLine ("Selecting device {0}", _selectedDevice.FriendlyName);
			ConnectToDevice ();
		}

		void OnDisconnect ()
		{
			Console.WriteLine ("Disconnecting device {0}", _selectedDevice.FriendlyName);
			_deviceManager.LeaveApplication ();
			_deviceManager.Disconnect ();

			DeviceDisconnected ();
			UpdateButtonStates();
		}

		void DeviceDisconnected()
		{
			_applicationStarted = false;
			_hasJoined = false;

			_channel.Dispose ();
			_channel = null;

			_deviceManager.Dispose ();
			_deviceManager = null;

			_selectedDevice.Dispose ();
			_selectedDevice = null;
		}

		void ConnectToDevice () 
		{
			if (_selectedDevice == null)
				return;
			
			var info = NSBundle.MainBundle.InfoDictionary;

			_deviceManager = new GCKDeviceManager (_selectedDevice, info ["CFBundleIdentifier"].ToString ());

			var deviceManagerDelegate = new DeviceManagerDelegate {
				OnDidConnect = DidConnect,
				OnDidConnectToCastApplication = DidConnectToCastApplication
			};
			_deviceManager.Delegate = deviceManagerDelegate;
			_deviceManager.Connect ();
		}

		void DidConnect (GCKDeviceManager deviceManager)
		{
			UpdateButtonStates ();
			deviceManager.LaunchApplication (CAST_APP_ID);
		}

		void DidConnectToCastApplication (GCKDeviceManager deviceManager, GCKApplicationMetadata applicationMetadata, string sessionId, bool launchedApplication)
		{
			Console.WriteLine("application name: {0}, sessionId: {1}, wasLaunched: {2}",
				applicationMetadata.ApplicationName,
				sessionId,
				launchedApplication);

			_channel = new MyChannel { OnDidReceiveTextMessage = DidReceiveTextMessage };

			_deviceManager.AddChannel (_channel);

			_applicationStarted = true;

			UpdateButtonStates ();

			textName.BecomeFirstResponder ();
		}

		void DidReceiveTextMessage(string message)
		{
			Console.WriteLine ("DidReceiveTextMessage: {0}", message);
		}
	}
}

