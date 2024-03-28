using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance;
    [SerializeField] private StoryObject _currentObject; public StoryObject CurrentObject {  get { return _currentObject; } }
    [SerializeField] private GameObject _buttonPrefab;

    [SerializeField] private Transform _choicesParent;
    [SerializeField] private Image _characterImage;

    private Coroutine _goToNextAutomaticallyCoroutine;

    private bool _choiceDisplayed = false;

    public Dictionary<string, int> StoryItems = new Dictionary<string, int>(); 

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ReadStoryObject();
        InputManager.Instance.OnClickEvent.AddListener(OnPlayerClick);
    }

    private void Update()
    {
        if (!_currentObject)
        {
            Debug.LogError("There's no current Story object !");
            return;
        }

        if (_currentObject.HasChoices && !_choiceDisplayed && TextBoxManager.Instance.FinishedTalking)
        {
            DisplayChoice();
        }
    }

    private void DisplayChoice()
    {
        _choiceDisplayed = true;
        for (int i = 0; i < _currentObject.Choices.Length; i++)
        {
            StoryObject newStory = _currentObject.Choices[i];
            if (newStory)
            {
                GameObject choiceButton = Instantiate(_buttonPrefab, _choicesParent);
                if (ReadChoiceConditions(i))
                {
                    choiceButton.GetComponent<Button>().onClick.AddListener(delegate { GoToStoryObject(newStory); });
                    if (_currentObject.ChoicesItemChange.Count > i && _currentObject.ChoicesItemChange[i].Items.Count > 0)
                    {
                        List<string> choiceItems = _currentObject.ChoicesItemChange[i].Items;
                        choiceButton.GetComponent<Button>().onClick.AddListener(delegate { ReadStoryItems(choiceItems); });
                    }
                }
                choiceButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = _currentObject.ChoicesText[i];
            }
        }
    }

    private bool ReadChoiceConditions(int index) //ITEM, OPERATOR, QUANTITY
    {
        bool canChoose = true;
        List<StoryConditionWrapper> storyConditions = _currentObject.ChoicesConditions; 

        if (index < storyConditions.Count && storyConditions[index].conditions.Count > 0)
        {
            List<string> conditions = storyConditions[index].conditions;
            for (int i = 0; i < conditions.Count;i++)
            {
                string currentCondition = conditions[i];
                string conditionOperator = GetFirstArgumentInOrder(currentCondition); currentCondition = RemoveFirstArgumentInOrder(currentCondition);
                int conditionQuantity = int.Parse(GetFirstArgumentInOrder(currentCondition)); currentCondition = RemoveFirstArgumentInOrder(currentCondition);

                int itemQuantity = 0;
                if (StoryItems.ContainsKey(currentCondition)) itemQuantity = StoryItems[currentCondition];


                switch (conditionOperator)
                {
                    case "<":
                        if (!(itemQuantity < conditionQuantity)) canChoose = false;
                        break;
                    case ">":
                        if (!(itemQuantity > conditionQuantity)) canChoose = false;
                        break;
                    case "<=":
                        if (!(itemQuantity <= conditionQuantity)) canChoose = false;
                        break;
                    case ">=":
                        if (!(itemQuantity >= conditionQuantity)) canChoose = false;
                        break;
                    case "==":
                        if (!(itemQuantity == conditionQuantity)) canChoose = false;
                        break;
                }
            }
        }

        return canChoose;
    }

    private void DestroyChoices()
    {
        for (int i = 0; i < _choicesParent.childCount; i++)
        {
            Destroy(_choicesParent.GetChild(i).gameObject);
        }
    }

    public void OnPlayerClick()
    {
        if (TextBoxManager.Instance.FinishedTalking)
        {
            if (_currentObject.CanClickToNext && !_currentObject.HasChoices)
            {
                GoToNextStoryObject();
            }
        }
        else
        {
            TextBoxManager.Instance.FinishTalking();
        }
    }

    public void ReadStoryObject()
    {
        _choiceDisplayed = false;
        TextBoxManager.Instance.SetText(_currentObject.StoryText, _currentObject.Character.FirstName);
        if (_currentObject.Character.CharacterSprite) _characterImage.sprite = _currentObject.Character.CharacterSprite;
        ReadStoryItems(_currentObject.Items);

        if (_currentObject.PlayVideo && _currentObject.VideoClip) //video
        {
            VideoManager.Instance.PlayVideo(_currentObject.VideoClip);
            VideoManager.Instance.FinishedPlayingEvent.AddListener(NextOnVideoEnd);
        }
        else
        {
            if (_currentObject.NextAutomatically)
            {
                _goToNextAutomaticallyCoroutine = StartCoroutine(GoToNextAutomatically());
            }

            if (_currentObject.HasChoices)
            {
            }
        }
    }

    private void ReadStoryItems(List<string> items) //ITEM, OPERATOR, QUANTITY
    {
        for (int i = 0; i < items.Count; i++)
        {
            string currentItem = items[i];
            string itemOperator = GetFirstArgumentInOrder(currentItem); currentItem = RemoveFirstArgumentInOrder(currentItem);
            int itemQuantity = int.Parse(GetFirstArgumentInOrder(currentItem)); currentItem = RemoveFirstArgumentInOrder(currentItem);

            if (!StoryItems.ContainsKey(currentItem))
            {
                StoryItems[currentItem] = 0;
            }

            switch (itemOperator)
            {
                case "+":
                    ItemFeedbackManager.Instance.SendFeedback(currentItem + ": " + (StoryItems[currentItem] + itemQuantity));
                    StoryItems[currentItem] += itemQuantity;
                    break;
                case "-":
                    ItemFeedbackManager.Instance.SendFeedback(currentItem + ": " + (StoryItems[currentItem] - itemQuantity));
                    StoryItems[currentItem] -= itemQuantity;
                    break;
                case "*":
                    ItemFeedbackManager.Instance.SendFeedback(currentItem + ": " + (StoryItems[currentItem] * itemQuantity));
                    StoryItems[currentItem] *= itemQuantity;
                    break;
                case "/":
                    ItemFeedbackManager.Instance.SendFeedback(currentItem + ": " + (StoryItems[currentItem] / itemQuantity));
                    StoryItems[currentItem] /= itemQuantity;
                    break;
            }
        }
    }

    private IEnumerator GoToNextAutomatically()
    {
        if (_currentObject.WaitForFinishedTalking)
        {
            while (!TextBoxManager.Instance.FinishedTalking)
            {
                yield return null;
            }
        }
        
        yield return new WaitForSeconds(_currentObject.NextStoryInSeconds);

        _goToNextAutomaticallyCoroutine = null;
        GoToNextStoryObject();
        yield return null;
    }

    public void NextOnVideoEnd()
    {
        VideoManager.Instance.FinishedPlayingEvent.RemoveListener(NextOnVideoEnd);
        GoToNextStoryObject();
    }

    public void GoToStoryObject(StoryObject story)
    {
        DestroyChoices();
        if (_goToNextAutomaticallyCoroutine != null) StopCoroutine(_goToNextAutomaticallyCoroutine);

        _currentObject = story;
        ReadStoryObject();
    }

    public void GoToNextStoryObject()
    {
        DestroyChoices();
        if (_goToNextAutomaticallyCoroutine != null) StopCoroutine(_goToNextAutomaticallyCoroutine);

        _currentObject = _currentObject.NextStory;
        ReadStoryObject();
    }
    
    public void ReceiveOrder(string order)
    {
        string fullOrder = order;
        if (order.Contains("SFX=")) //PATH, VOLUME, CATEGORY, STOP CATEGORY, DELAY
        {
            order = order.Replace("SFX=", "");
            float volume = 1;
            float delay = 0;
            string category = "none";
            string stopCategory = "N/A";

            if (OrderHasArgument(order)) volume = float.Parse(GetFirstArgumentInOrder(order).Replace(".", ",")); order = RemoveFirstArgumentInOrder(order);
            if (OrderHasArgument(order)) category = GetFirstArgumentInOrder(order); order = RemoveFirstArgumentInOrder(order);
            if (OrderHasArgument(order)) stopCategory = GetFirstArgumentInOrder(order); order = RemoveFirstArgumentInOrder(order);
            if (OrderHasArgument(order)) delay = float.Parse(GetFirstArgumentInOrder(order).Replace(".", ",")); order = RemoveFirstArgumentInOrder(order);
            order = RemoveAllArgumentsInOrder(order);

            if (stopCategory != "N/A") SoundManager.Instance.StopAllFromCategory(stopCategory);
            SoundManager.Instance.PlayAtPath("SFX/" + order, volume, 0, delay, category);
        }
        if (order.Contains("SFXSTOP=")) //CATEGORY
        {
            order = order.Replace("SFXSTOP=", "");
            order = RemoveAllArgumentsInOrder(order);
            SoundManager.Instance.StopAllFromCategory(order);
        }
        if (order.Contains("MUSIC=")) //PATH, VOLUME
        {
            order = order.Replace("MUSIC=", "");
            float volume = 1;

            if (OrderHasArgument(order)) volume = float.Parse(GetFirstArgumentInOrder(order).Replace(".", ",")); order = RemoveFirstArgumentInOrder(order);
            order = RemoveAllArgumentsInOrder(order);

            SoundManager.Instance.PlayMusicAtPath("MUSIC/" + order, volume);
        }
        if (order.Contains("STORYOBJECT="))
        {
            order = order.Replace("STORYOBJECT=", "");

            order = RemoveAllArgumentsInOrder(order);

            _currentObject = Resources.Load<StoryObject>("Story/Objects/" + order);
            ReadStoryObject();
        }
    }

    private bool OrderHasArgument(string order)
    {
        return order.Contains(',');
    }
    private string GetFirstArgumentInOrder(string order)
    {
        int index = order.IndexOf(',');
        order = order.Remove(0, index + 1);
        order = order.Replace(" ", "");
        if (order.Contains(','))
        {
            order = order.Remove(order.IndexOf(","), order.Length - order.IndexOf(","));
        }
        return order;
    }

    private string RemoveFirstArgumentInOrder(string order)
    {
        if (!OrderHasArgument(order)) return order;

        int index = order.IndexOf(',');
        if (order.LastIndexOf(",") == index)
        {
            order = order.Remove(index, order.Length - index);
        }
        else
        {
            int nextIndex = 0;
            for (int i = index + 1; i < order.Length; i++)
            {
                if (order[i] == ',')
                {
                    nextIndex = i;
                    break;
                }
            }
            order = order.Remove(index, nextIndex - index);
        }

        return order;
    }

    private string RemoveAllArgumentsInOrder(string order)
    {
        while (OrderHasArgument(order)) 
        {
            order = RemoveFirstArgumentInOrder(order);
        }
        return order;
    }
}