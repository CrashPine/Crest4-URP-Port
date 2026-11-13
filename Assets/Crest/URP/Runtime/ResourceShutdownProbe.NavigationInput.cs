// Observes native InputSystem releases; never generates input or navigation. MIT.
#if CREST_RESOURCE_PROBE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crest.URP.Diagnostics
{
    public sealed partial class ResourceShutdownProbe
    {
        [Serializable] class NavigationInputRow
        {
            public string key,configuration,keyboard,layout;public int previous,index,active,frame,deviceId;public bool focused,native;
        }
        [Serializable] class NavigationInputReport
        {
            public string unity,api,gpu,scope,phase,failure,initialConfiguration;public int errors,initialIndex;
            public List<NavigationInputRow> rows=new List<NavigationInputRow>();
        }
        NavigationInputReport _navigationInput;Transform _navigationRoot;Type _navigationKeyboardType;int _navigationPrevious;
        void SaveNavigationInput()
        {
            if(_navigationInput==null)return;_navigationInput.errors=_errors;
            File.WriteAllText(Argument("-crestProbeOutput","NavigationInput.json"),JsonUtility.ToJson(_navigationInput,true));
        }
        static object NavigationProperty(object value,string name)=>value?.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance)?.GetValue(value);
        int NavigationActiveIndex()
        {
            var active=_navigationRoot.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy).ToArray();CheckExample(active.Length==1,"Native navigation must retain one active configuration");return active[0].GetSiblingIndex();
        }
        void ObserveNavigationInput()
        {
            if(_navigationInput==null||_navigationInput.phase!="ready"||_navigationRoot==null)return;
            try
            {
                var keyboard=_navigationKeyboardType.GetProperty("current",BindingFlags.Public|BindingFlags.Static).GetValue(null);if(keyboard==null)return;
                foreach(string key in new[]{"m","n"})
                {
                    var control=NavigationProperty(keyboard,key+"Key");if(NavigationProperty(control,"wasReleasedThisFrame") is not bool released||!released)continue;
                    int index=NavigationActiveIndex();var row=new NavigationInputRow{key=key,previous=_navigationPrevious,index=index,active=1,configuration=_navigationRoot.GetChild(index).name,frame=Time.frameCount,focused=Application.isFocused,native=(bool)NavigationProperty(keyboard,"native"),keyboard=NavigationProperty(keyboard,"displayName")?.ToString(),layout=NavigationProperty(keyboard,"layout")?.ToString(),deviceId=(int)NavigationProperty(keyboard,"deviceId")};
                    _navigationInput.rows.Add(row);SaveNavigationInput();
                    string expectedKey=_navigationInput.rows.Count==1?"m":"n";int expected=(_navigationPrevious+(key=="m"?1:23))%24;
                    CheckExample(_navigationInput.rows.Count<=2&&key==expectedKey&&row.native&&row.focused&&index==expected,"Actual native key release did not reach original controller");_navigationPrevious=index;
                    if(_navigationInput.rows.Count==2){CheckExample(index==_navigationInput.initialIndex,"N must restore initial configuration");_navigationInput.phase="complete";SaveNavigationInput();}
                }
            }
            catch(Exception error){_navigationInput.failure=error.ToString();_navigationInput.phase="failed";Debug.LogException(error);SaveNavigationInput();}
        }
        IEnumerator NavigationInputPlayer()
        {
            _navigationInput=new NavigationInputReport{unity=Application.unityVersion,api=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName,phase="loading",scope="P11.3-G1a only: original Examples focused native M thenN releases via OS UI event path, observed after source Update. No virtual device/QueueEvent/direct Next/Previous. Not human/physical USB, debug GUI, Inspector/GPU update or all24 traversal."};SaveNavigationInput();
            CheckExample(!Application.isBatchMode,"Navigation input requires ordinary interactive Player");yield return SceneManager.LoadSceneAsync(1);
            var controllers=FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(c=>c.GetType().FullName=="Crest.Examples.ExamplesController").ToArray();
            _navigationRoot=controllers.Single(c=>c.transform.childCount==24&&c.transform.GetChild(0).name=="Albedo").transform;
            _navigationKeyboardType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.InputSystem.Keyboard")).FirstOrDefault(t=>t!=null);CheckExample(_navigationKeyboardType!=null,"Native input package missing");
            for(int warm=0;warm<45;warm++)yield return null;
            _navigationPrevious=NavigationActiveIndex();_navigationInput.initialIndex=_navigationPrevious;_navigationInput.initialConfiguration=_navigationRoot.GetChild(_navigationPrevious).name;_navigationInput.phase="ready";SaveNavigationInput();
            float deadline=Time.realtimeSinceStartup+180;
            while(_navigationInput.phase=="ready"&&Time.realtimeSinceStartup<deadline)yield return null;
            CheckExample(_navigationInput.phase=="complete"&&_navigationInput.rows.Count==2,"Native keyboard controls incomplete: "+_navigationInput.failure);
            // Finish is lifecycle-only; it never advances or fabricates acceptance.
            while(!File.Exists(Argument("-crestInteractionFinish","NavigationInput.finish"))&&Time.realtimeSinceStartup<deadline)yield return null;
            CheckExample(File.Exists(Argument("-crestInteractionFinish","NavigationInput.finish")),"UI observation handoff timed out");
            SaveNavigationInput();Debug.Log("NAVIGATION_INPUT_COMPLETED errors="+_errors);Application.Quit(_errors==0?0:1);
        }
    }
}
#endif
