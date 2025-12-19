
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Data;
using TMPro;
using VRC.SDK3.Components;
using VRC.SDK3.UdonNetworkCalling;

namespace CameraSystem {
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class CameraSystem_Console : UdonSharpBehaviour {
		[Header("Cameras")]
		public Camera[] camerasObjects;
		public RenderTexture[] camerasRenderTextures;
		public Material[] camerasMaterials;

		[Header("Controls and UI")]
		public Image[] sendLiveButtons;
		public TextMeshProUGUI currentCameraText;
		public TextMeshProUGUI[] deskCamerasFovTexts;
		public TextMeshProUGUI[] handheldsFovTexts;
		public TextMeshProUGUI[] followUsernameTexts;
		public VRCPickup[] handheldsVrcPickups;
		public GameObject initErrorWarningText;
		public Slider[] cameraFovSliders;
		public Button potatoButton;
		// TODO prev btns, next btns, current username, selected username

		[Header("Feedbacks")]
		public Material liveMaterial;
		public Material cameraJack;

		[HideInInspector] [UdonSynced] public int currentCamera = 0;
		private int lastCamera = 0;
		[HideInInspector] [UdonSynced] public string[] cameraFollowUsername = new string[6];
		[HideInInspector] [UdonSynced] public bool[] cameraFollow = new bool[6];
		[HideInInspector] [UdonSynced] private string _jsonPlayersList = "";
		private DataList _playersList = new DataList();
		[UdonSynced] public float[] cameraFOV = new float[6];

		[Header("State colors")]
		public Color32 colorGreen = new Color(15/255f, 132/255f, 12/255f, 255/255f);
		public Color32 colorGrey = new Color(255f, 255f, 255f, 255/255f);
		public Color32 colorRed = new Color(255f, 0/255f, 0/255f, 255/255f);
		public Color32 colorBlack = new Color(0f, 0f, 0f, 255/255f);

		private bool isAuthorized = false;
		private bool potato = false;


		public void Authorize() {
			noLongerAPotato();
			potatoButton.enabled = false;
			Debug.Log($"[OTT_CAMERA_SYSTEM][authorize] User is now authorized to use the console");
			isAuthorized = true;
			foreach (VRCPickup vrcp in handheldsVrcPickups) {
				if (Utilities.IsValid(vrcp)) {
					//vrcp.pickupable = true;
				}
			}
		}

		public void Deauthorize() {
			Debug.Log($"[OTT_CAMERA_SYSTEM][deauthorize] User is now forbidden to have fun");
			isAuthorized = false;
			foreach (VRCPickup vrcp in handheldsVrcPickups) {
				if (Utilities.IsValid(vrcp)) {
					vrcp.pickupable = false;
				}
			}
			potatoButton.enabled = true;
		}

		void Start() {
			Debug.Log($"[OTT_CAMERA_SYSTEM][Start] FOV of camera 1 is " + cameraFOV[0]);
			
			if (!sanityCheck()) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][Start] Sanity check failed");
				return; // everything will be broken anyway
			}

			foreach (Image img in sendLiveButtons) {
				img.color = colorGrey;
			}

			// Set the first camera as live
			SendLiveCamera(0);

			updateCameraFovs();

			// and disable pickupables
			foreach (VRCPickup vrcp in handheldsVrcPickups) {
				if (Utilities.IsValid(vrcp)) {
					vrcp.pickupable = false;
				}
			}

			Authorize();
		}

		// Quick check for validity of all our basic needed objects
		private bool sanityCheck() {
			// TODO check everything + arrays > 0
			bool error = false;
			foreach (Camera cam in camerasObjects) {
				if (!Utilities.IsValid(cam)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the camerasObjects list");
					error = true;
				}
			}
			foreach (RenderTexture rt in camerasRenderTextures) {
				if (!Utilities.IsValid(rt)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the camerasRenderTextures list");
					error = true;
				}
			}
			foreach (Material mat in camerasMaterials) {
				if (!Utilities.IsValid(mat)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the camerasMaterials list");
					error = true;
				}
			}
			foreach (Image img in sendLiveButtons) {
				if (!Utilities.IsValid(img)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the sendLiveButtons list");
					error = true;
				}
			}
			foreach (Slider sl in cameraFovSliders) {
				if (!Utilities.IsValid(sl)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the cameraFovSliders list");
					error = true;
				}
			}
			if (!Utilities.IsValid(liveMaterial)) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Invalid liveMaterial");
				error = true;
			}
			if (!Utilities.IsValid(cameraJack)) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Invalid cameraJack");
				error = true;
			}
			if (!Utilities.IsValid(currentCameraText)) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Invalid currentCameraText");
				error = true;
			}

			if (error && Utilities.IsValid(initErrorWarningText)) {
				initErrorWarningText.SetActive(true);
			}
			return !error;
		}

		public void TogglePotato() {
			potato = !potato;
			if (potato) {
				iAmAPotato();
			} else {
				noLongerAPotato();
			}
		}

		private void noLongerAPotato() {
			Debug.Log($"[OTT_CAMERA_SYSTEM][noLongerAPotato]");
			for (int i = 0; i < 6; i++) {
				camerasObjects[i].enabled = true;
			}
			potatoButton.GetComponent<Image>().color = colorGrey;
		}

		private void iAmAPotato() {
			Debug.Log($"[OTT_CAMERA_SYSTEM][iAmAPotato]");
			// For each Camera, UNLESS current camera, disable it
			for (int i=0; i<6; i++) {
				if (currentCamera != i) {
					camerasObjects[i].enabled = false;
				}
			}
			potatoButton.GetComponent<Image>().color = colorGreen;
		}

		public void SendLiveCamera(int index) {
			if (isAuthorized)
			{
				// Set the previous button to grey
				sendLiveButtons[lastCamera].color = colorGrey;
				// Set the new button to red
				sendLiveButtons[index].color = colorRed;
				// Set the live material to the right render textures
				liveMaterial.SetTexture("_MainTex", camerasRenderTextures[index]);
				liveMaterial.SetTexture("_EmissionMap", camerasRenderTextures[index]);
				cameraJack.SetTexture("_MainTex", camerasRenderTextures[index]);
				// Set the text for the current camera name
				currentCameraText.text = $"Camera {index + 1}";

				// Finally set the current camera index
				currentCamera = index;
				lastCamera = index;

				// Update potato cameras if needed
				if (potato)
				{
					iAmAPotato();
				}

				Networking.SetOwner(Networking.LocalPlayer, gameObject);
				RequestSerialization();
			}
		}

		public void SendLiveCamera1() {
			if (isAuthorized) {
				SendLiveCamera(0);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera1] Unauthorized action.");
			}
		}

		public void SendLiveCamera2() {
			if (isAuthorized) {
				SendLiveCamera(1);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera2] Unauthorized action.");
			}
		}

		public void SendLiveCamera3() {
			if (isAuthorized) {
				SendLiveCamera(2);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera3] Unauthorized action.");
			}
		}

		public void SendLiveCamera4() {
			if (isAuthorized) {
				SendLiveCamera(3);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera4] Unauthorized action.");
			}
		}

		public void SendLiveCamera5() {
			if (isAuthorized) {
				SendLiveCamera(4);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera5] Unauthorized action.");
			}
		}

		public void SendLiveCamera6() {
			if (isAuthorized) {
				SendLiveCamera(5);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera6] Unauthorized action.");
			}
		}

		public override void OnPlayerJoined(VRCPlayerApi player) {
			if (!Utilities.IsValid(player)) { return; }
			addPlayer(player);
		}

		public override void OnPlayerLeft(VRCPlayerApi player) {
			if (!Utilities.IsValid(player)) { return; }
			// remove player from any listing if they leave the world
			removePlayer(player);
		}
		
		public override void OnPreSerialization() {
			if (lastCamera != currentCamera) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][OnDeserialization] Last Camera: {lastCamera}, New Camera: {currentCamera}");
				SendLiveCamera(currentCamera);
			}

			// Convert the list _playersList to the network-synced variable _jsonPlayersList
			if (VRCJson.TrySerializeToJson(_playersList, JsonExportType.Minify, out DataToken result)) {
				Debug.Log($"[OTT_CAMERA_SYSTEM] OnPreSerialization: {_playersList.ToArray()} -> {result.String}");
				_jsonPlayersList = result.String;
			} else {
				Debug.LogError($"[OTT_CAMERA_SYSTEM] OnPreSerialization error: {result.ToString()}");
			}
			// No update because we are PreSerializing from RequestSerialization and updatePlayerlist is called after it
		}

		public override void OnDeserialization() {
			// Convert the network-synced variable _jsonPlayersList and convert it to a list in _playersList
			if(VRCJson.TryDeserializeFromJson(_jsonPlayersList, out DataToken result)) {
				Debug.Log($"[OTT_CAMERA_SYSTEM] OnDeserialization: {_jsonPlayersList} -> {result.DataList.ToArray()}");
				_playersList = result.DataList;
			} else {
				Debug.LogError($"[OTT_CAMERA_SYSTEM] OnDeserialization error: {result.ToString()}");
			}
			// Then update the players list
			updatePlayerlists();
			updateCameraFovs();
			SendLiveCamera(currentCamera);
		}

		public void addPlayer(VRCPlayerApi player) {
			// Invalid or already contains the user ? skip
			if (!Utilities.IsValid(player)) { return; }
			if (_playersList.Contains(player.displayName)) { return; }

			_playersList.Add((string)player.displayName);
			Debug.Log($"[OTT_CAMERA_SYSTEM] Player '{player.displayName}' added in the list");

			// Only set ownership right before RequestSerialization and after checking if we really need to add the user
			// to avoid spamming ownership transfert (which makes nothing work properly)
			Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			RequestSerialization();
			// Workaround for https://github.com/vrchat-community/ClientSim/issues/74
#if UNITY_EDITOR
			Debug.Log($"[OTT_CAMERA_SYSTEM] Running UNITY_EDITOR only workaround");
			OnPreSerialization();
#endif
			updatePlayerlists();
		}

		public void removePlayer(VRCPlayerApi player) {
			// Invalid or not in the list ? skip
			if (!Utilities.IsValid(player)) { return; }
			if (!_playersList.Contains(player.displayName)) { return; }

			_playersList.Remove((string)player.displayName);
			Debug.Log($"[OTT_CAMERA_SYSTEM] Player '{player.displayName}' removed from the list");

			// Only set ownership right before RequestSerialization and after checking if we really need to remove the user
			// to avoid spamming ownership transfert (which makes nothing work properly)
			Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			RequestSerialization();
			// Workaround for https://github.com/vrchat-community/ClientSim/issues/74
#if UNITY_EDITOR
			Debug.Log($"[OTT_CAMERA_SYSTEM] Running UNITY_EDITOR only workaround");
			OnPreSerialization();
#endif
			updatePlayerlists();
		}

		public void _setSelectedUsername(string player, int cameraId) {
			if (!isAuthorized) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][_setSelectedUsername] Unauthorized action.");
				return;
			}
			Debug.Log($"[OTT_CAMERA_SYSTEM][_setSelectedUsername] Setting follow user {player} for camera {cameraId}");
			switch (cameraId) {
				case 1:
					cameraFollowUsername[0] = player;
					break;
				case 2:
					cameraFollowUsername[1] = player;
					break;
				case 3:
					cameraFollowUsername[2] = player;
					break;
				case 4:
					cameraFollowUsername[3] = player;
					break;
				case 5:
					cameraFollowUsername[4] = player;
					break;
				case 6:
					cameraFollowUsername[5] = player;
					break;
			}
			Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			RequestSerialization();
		}

		void updatePlayerlists() {
			// TODO just reset the "current username" field with the first user in the list
		}

		private void updateCameraFovs() {
			//Debug.Log($"[OTT_CAMERA_SYSTEM][updateCameraFovs]");
			//camerasObjects[0].fieldOfView = cameraFOV[0];
			//camerasObjects[1].fieldOfView = cameraFOV[1];
			//camerasObjects[2].fieldOfView = cameraFOV[2];
			//camerasObjects[3].fieldOfView = cameraFOV[3];
			//camerasObjects[4].fieldOfView = cameraFOV[4];
			//camerasObjects[5].fieldOfView = cameraFOV[5];
			
			//deskCamerasFovTexts[0].text = $"FOV {cameraFOV[0]}";
			//deskCamerasFovTexts[1].text = $"FOV {cameraFOV[1]}";
			//deskCamerasFovTexts[2].text = $"FOV {cameraFOV[2]}";
			//deskCamerasFovTexts[3].text = $"FOV {cameraFOV[3]}";
			//deskCamerasFovTexts[4].text = $"FOV {cameraFOV[4]}";
			//deskCamerasFovTexts[5].text = $"FOV {cameraFOV[5]}";
			
			//handheldsFovTexts[0].text = $"FOV {cameraFOV[0]}";
			//handheldsFovTexts[1].text = $"FOV {cameraFOV[1]}";
			//handheldsFovTexts[2].text = $"FOV {cameraFOV[2]}";
			//handheldsFovTexts[3].text = $"FOV {cameraFOV[3]}";
			//handheldsFovTexts[4].text = $"FOV {cameraFOV[4]}";
			//handheldsFovTexts[5].text = $"FOV {cameraFOV[5]}";
		}

		public void _camera1FovChanged() {
			if (!isAuthorized) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][_camera1FovChanged] Unauthorized action.");
				cameraFovSliders[0].value = cameraFOV[0];
				return;
			}
			cameraFOV[0] = cameraFovSliders[0].value;
			Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			RequestSerialization();
			updateCameraFovs();
		}

		public void _camera2FovChanged() {
			if (!isAuthorized) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][_camera2FovChanged] Unauthorized action.");
				cameraFovSliders[1].value = cameraFOV[1];
				return;
			}
			cameraFOV[1] = cameraFovSliders[1].value;
			Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			RequestSerialization();
			updateCameraFovs();
		}

		public void _camera3FovChanged() {
			if (!isAuthorized) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][_camera3FovChanged] Unauthorized action.");
				cameraFovSliders[2].value = cameraFOV[2];
				return;
			}
			cameraFOV[2] = cameraFovSliders[2].value;
			Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			RequestSerialization();
			updateCameraFovs();
		}

		public void _camera4FovChanged() {
			if (!isAuthorized) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][_camera4FovChanged] Unauthorized action.");
				cameraFovSliders[3].value = cameraFOV[3];
				return;
			}
			cameraFOV[3] = cameraFovSliders[3].value;
			Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			RequestSerialization();
			updateCameraFovs();
		}

		public void _camera5FovChanged() {
			if (!isAuthorized) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][_camera5FovChanged] Unauthorized action.");
				cameraFovSliders[4].value = cameraFOV[4];
				return;
			}
			cameraFOV[4] = cameraFovSliders[4].value;
			Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			RequestSerialization();
			updateCameraFovs();
		}

		public void _camera6FovChanged() {
			if (!isAuthorized) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][_camera6FovChanged] Unauthorized action.");
				cameraFovSliders[5].value = cameraFOV[5];
				return;
			}
			cameraFOV[5] = cameraFovSliders[5].value;
			Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
			RequestSerialization();
			updateCameraFovs();
		}
	}
}
