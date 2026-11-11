// 마중불 검증용 Unity API 스텁 (tools/verify/verify.sh 전용, Unity 프로젝트에 포함되지 않는다)
//
// Assets/Scripts/House/*.cs 와 Assets/Editor/HouseSceneBuilder.cs 가 쓰는 UnityEngine·UnityEditor API를
// 이름과 시그니처만 흉내 낸다. 동작은 없다(대부분 빈 몸체나 기본값을 돌려준다).
// 목적은 mcs 로 "타입 검사"를 하는 것뿐이다. 여기서 컴파일된다고 Unity에서 동작한다는 뜻은 아니다.
//
// House 코드가 새 Unity API를 쓰기 시작해 verify.sh 가 "형식 또는 멤버를 찾을 수 없다"(CS0117, CS0246, CS1061 등)로
// 실패하면, 그 API가 실제 Unity 6에 있는 이름·시그니처인지 Unity 문서로 확인한 뒤에만 여기에 추가한다.
// 없는 API를 스텁에 넣으면 Unity에서 깨질 코드를 통과시키게 된다.
//
// 맨 아래 Majungbul.Sfx 는 실제 Assets/Scripts/Sfx.cs 대신 쓰는 스텁이다. 원본이 C# 8 문법(??=)을 써서
// mcs(C# 7까지 지원)로 컴파일되지 않기 때문이다. Sfx 의 공개 속성이 바뀌면 여기도 맞춘다.

using System; using System.Collections;
namespace UnityEngine {
  public enum FindObjectsSortMode { None }
  public class Object { public string name; public static T FindAnyObjectByType<T>() where T:Object=>default(T); public static T[] FindObjectsByType<T>(FindObjectsSortMode m) where T:Object=>null; public static void DestroyImmediate(Object o){} }
  public class Component : Object { public GameObject gameObject; public Transform transform; public T GetComponent<T>()=>default(T); public T[] GetComponentsInChildren<T>()=>null; }
  public class Behaviour : Component { public bool enabled; public bool isActiveAndEnabled; }
  public class Coroutine {} public class YieldInstruction {}
  public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s){} }
  public class WaitForSecondsRealtime : YieldInstruction { public WaitForSecondsRealtime(float s){} }
  public class MonoBehaviour : Behaviour { public Coroutine StartCoroutine(IEnumerator e)=>null; public void StopCoroutine(Coroutine c){} public void StopAllCoroutines(){} }
  public class Transform : Component { public Vector3 position, localPosition, localScale; public Quaternion localRotation; public void SetParent(Transform p, bool w){} }
  public class RectTransform : Transform { public Vector2 anchorMin, anchorMax, offsetMin, offsetMax; }
  public class GameObject : Object { public GameObject(string n){} public GameObject(string n, params Type[] t){} public T AddComponent<T>() where T:Component=>default(T); public T GetComponent<T>()=>default(T); public Transform transform; public void SetActive(bool b){} public bool activeSelf; public string tag; }
  public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} public static Vector2 zero, one; public static Vector2 operator*(Vector2 a,float f)=>a; public static Vector2 operator/(Vector2 a,float f)=>a; public static Vector2 operator-(Vector2 a)=>a; public static Vector2 operator-(Vector2 a, Vector2 b)=>a; public static Vector2 operator+(Vector2 a, Vector2 b)=>a; public static float Distance(Vector2 a, Vector2 b)=>0; }
  public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public static Vector3 up; public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a; public static Vector3 operator+(Vector3 a,Vector3 b)=>a; public static Vector3 operator*(Vector3 a,float f)=>a; }
  public struct Quaternion { public static Quaternion identity; public static Quaternion Euler(float x,float y,float z)=>identity; }
  public struct Color { public float r,g,b,a; public Color(float r,float g,float b){this.r=r;this.g=g;this.b=b;a=1;} public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color black, white, clear; public static Color Lerp(Color a, Color b, float t)=>a; }
  public struct Vector2Int { public int x,y; public Vector2Int(int a,int b){x=a;y=b;} }
  public static class Mathf { public const float PI=3.14f; public static int FloorToInt(float f)=>0; public static float Max(float a,float b)=>a; public static int Abs(int a)=>a; public static float Abs(float a)=>a; public static float Lerp(float a,float b,float t)=>a; public static float Clamp(float v,float a,float b)=>v; public static int Clamp(int v,int a,int b)=>v; public static float Clamp01(float v)=>v; public static float Exp(float v)=>v; public static float Sin(float v)=>v; }
  public static class Time { public static float deltaTime, unscaledDeltaTime, unscaledTime; public static int frameCount; }
  public class Renderer : Component {} public class Sprite : Object {} public class Material : Object {}
  public class SpriteRenderer : Renderer { public Sprite sprite; public Color color; public int sortingOrder; public Material sharedMaterial; }
  public enum TextureFormat { RGBA32 } public enum FilterMode { Point, Bilinear }
  public class Texture : Object { public FilterMode filterMode; } public class Texture2D : Texture { public Texture2D(int w,int h,TextureFormat f,bool m){} public void SetPixel(int x,int y,Color c){} public void Apply(){} public byte[] EncodeToPNG()=>null; }
  public enum CameraClearFlags { SolidColor }
  public class Camera : Behaviour { public static Camera main; public bool orthographic; public float orthographicSize, aspect; public CameraClearFlags clearFlags; public Color backgroundColor; }
  public class AudioClip : Object { public static AudioClip Create(string n,int a,int b,int c,bool d)=>null; public bool SetData(float[] d,int o)=>true; }
  public class AudioSource : Behaviour { public AudioClip clip; public bool loop, playOnAwake; public float volume; public void Play(){} public void Stop(){} public void PlayOneShot(AudioClip c){} }
  public class Font : Object { public static Font CreateDynamicFontFromOSFont(string[] n,int s)=>null; }
  public static class Resources { public static T GetBuiltinResource<T>(string p) where T:Object=>default(T); }
  public static class Debug { public static void Log(object o){} public static void LogError(object o){} }
  public class SerializeField : Attribute {} public class HeaderAttribute : Attribute { public HeaderAttribute(string s){} }
  public class TextAreaAttribute : Attribute { public TextAreaAttribute(){} public TextAreaAttribute(int a,int b){} }
  public class RequireComponent : Attribute { public RequireComponent(Type t){} }
  public enum TextAnchor { UpperLeft, UpperRight, MiddleCenter, LowerRight, LowerLeft, LowerCenter }
  public enum HorizontalWrapMode { Wrap } public enum VerticalWrapMode { Overflow }
  public enum RenderMode { ScreenSpaceOverlay }
  public class Canvas : Behaviour { public RenderMode renderMode; public int sortingOrder; }
}
namespace UnityEngine.UI {
  public class CanvasScaler : Behaviour { public enum ScaleMode { ScaleWithScreenSize } public ScaleMode uiScaleMode; public Vector2 referenceResolution; public float matchWidthOrHeight; }
  public class Graphic : Behaviour { public Color color; public bool raycastTarget; }
  public class Text : Graphic { public Font font; public int fontSize; public TextAnchor alignment; public string text; public HorizontalWrapMode horizontalOverflow; public VerticalWrapMode verticalOverflow; }
  public class Image : Graphic {} public class RawImage : Graphic { public Texture texture; } public class Outline : Behaviour { public Color effectColor; }
}
namespace UnityEngine.InputSystem {
  public class KeyControl { public bool isPressed, wasPressedThisFrame; }
  public class Keyboard { public static Keyboard current; public KeyControl mKey,rKey,eKey,spaceKey,enterKey,upArrowKey,downArrowKey,leftArrowKey,rightArrowKey,wKey,aKey,sKey,dKey,leftShiftKey,rightShiftKey,escapeKey; }
}
namespace UnityEngine.SceneManagement {
  public struct Scene { public string name; }
  public static class SceneManager { public static void LoadScene(string n){} public static Scene GetActiveScene()=>default(Scene); }
}
namespace UnityEngine.Rendering.Universal {
  public class Light2D : Behaviour { public enum LightType { Parametric, Freeform, Sprite, Point = 3, Global = 4 } public LightType lightType; public Color color; public float intensity, pointLightOuterRadius, pointLightInnerRadius; }
}
namespace UnityEditor {
  using UnityEngine;
  public class MenuItem : System.Attribute { public MenuItem(string s){} }
  public static class EditorUtility { public static bool DisplayDialog(string a,string b,string c,string d)=>true; }
  public static class AssetDatabase { public static T LoadAssetAtPath<T>(string p) where T:Object=>default(T); public static bool DeleteAsset(string p)=>true; public static bool CopyAsset(string a,string b)=>true; public static void Refresh(){} }
  public class EditorBuildSettingsScene { public string path; public EditorBuildSettingsScene(string p,bool e){path=p;} }
  public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
  public static class Selection { public static GameObject activeGameObject; }
  public enum TextureImporterType { Sprite } public enum SpriteImportMode { Single }
  public class AssetImporter : Object { public static AssetImporter GetAtPath(string p)=>null; public void SaveAndReimport(){} }
  public class TextureImporter : AssetImporter { public TextureImporterType textureType; public SpriteImportMode spriteImportMode; public float spritePixelsPerUnit; public bool mipmapEnabled, alphaIsTransparency; public FilterMode filterMode; }
  public class SerializedProperty { public Object objectReferenceValue; public string stringValue; public int enumValueIndex, arraySize; public SerializedProperty GetArrayElementAtIndex(int i)=>this; }
  public class SerializedObject { public SerializedObject(Object o){} public SerializedProperty FindProperty(string f)=>null; public bool ApplyModifiedPropertiesWithoutUndo()=>true; }
}
namespace UnityEditor.SceneManagement {
  using UnityEngine.SceneManagement;
  public enum OpenSceneMode { Single } public enum NewSceneSetup { DefaultGameObjects } public enum NewSceneMode { Single }
  public static class EditorSceneManager { public static bool SaveCurrentModifiedScenesIfUserWantsTo()=>true; public static Scene OpenScene(string p, OpenSceneMode m)=>default(Scene); public static Scene NewScene(NewSceneSetup s, NewSceneMode m)=>default(Scene); public static bool MarkSceneDirty(Scene s)=>true; public static bool SaveScene(Scene s,string p)=>true; }
}
namespace Majungbul { public static class Sfx { public static UnityEngine.AudioClip Cup, Photo, Chime, Ignite, Douse; } }
