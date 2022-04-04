using LudumDare50.Prop;
using LudumDare50.SO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace LudumDare50.Player
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _debugText;

        [SerializeField]
        private PlayerInfo _info;

        [SerializeField]
        private LifespanBar _healthBar;

        [SerializeField]
        private LifespanBar _barFood, _barEntertainment, _barSmoke, _barAlcohol;

        [SerializeField]
        private Launcher _fridgeLauncher;

        [SerializeField]
        private Canvas _overlayCanvas;

        [SerializeField]
        private GameObject _fadingTextAnimationPrefab;

        public GameObject testImg;

        private Rigidbody _rb;

        private bool _isDisabled = false;

        private float _age;
        private readonly Dictionary<NeedType, float> _needs = new()
        {
            { NeedType.Food, .4f },
            { NeedType.Entertainment, .1f },
            { NeedType.Smoke, .1f },
            { NeedType.Alcohol, .1f }
        };

        private NavMeshAgent _agent;
        private Node _currNode;

        private NeedType MostNeeded => _needs.OrderByDescending(x => x.Value).First().Key;

        public void ReduceNeed(NeedType need)
        {
            _needs[need] = 0f;
        }

        private void Start()
        {
            _agent = GetComponent<NavMeshAgent>();
            _rb = GetComponent<Rigidbody>();

            _age = _info.MaxAge;
            UpdateDestination();
        }

        private void FixedUpdate()
        {
            // We are close enough to node, we are going to the next one
            if (!_isDisabled && Vector3.Distance(transform.position, _currNode.transform.position) < _info.MinDistBetweenNode)
            {
                ReduceNeed(_currNode.GivenNeed);
                if (_currNode.GivenNeed == NeedType.Food)
                {
                    _fridgeLauncher.Throw();
                }
                StartCoroutine(WaitAndReenablePlayer(2f));
            }
        }

        private void Update()
        {
            _age -= Time.deltaTime * _info.AgeProgression;
            var keys = _needs.Keys;
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                _needs[keys.ElementAt(i)] += Time.deltaTime * _info.NeedMultiplicator;
                if (_needs[keys.ElementAt(i)] > 1f)
                {
                    _needs[keys.ElementAt(i)] = 1f;
                }
            }
            UpdateUI();
        }

        private void UpdateDestination()
        {
            _age -= _info.AgeProgression;
            _currNode = ObjectiveManager.Instance.GetNextNode(MostNeeded);
            _agent.destination = _currNode.transform.position;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_debugText != null)
            {
                _debugText.text =
                    $"Age: {_age:0.00}\n" +
                    string.Join("\n", _needs.OrderByDescending(x => x.Value).Select(x => $"{x.Key}: {x.Value:0.00}"));
            }

            _healthBar.SetValue(_age / _info.MaxAge);

            _barEntertainment.SetValue(_needs[NeedType.Entertainment]);
            _barFood.SetValue(_needs[NeedType.Food]);
            _barSmoke.SetValue(_needs[NeedType.Smoke]);
            _barAlcohol.SetValue(_needs[NeedType.Alcohol]);
        }

        public void OnDrawGizmos()
        {
            if (_currNode == null)
            {
                return;
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, _currNode.transform.position);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TriggerCollisionEffects(collision.collider.tag);

            if (collision.collider.CompareTag("Food"))
            {                
                // Eat food we collide with
                _needs[NeedType.Food] -= _info.FoodPower;
                if (_needs[NeedType.Food] < 0f)
                {
                    _needs[NeedType.Food] = 0f;
                }
                Destroy(collision.gameObject);
                UpdateDestination();
            }
            else
            {
                var rb = collision.collider.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    if (rb.velocity.magnitude > 10f)
                    {
                        // Stun player
                        _rb.isKinematic = false;
                        _agent.enabled = false;
                        StartCoroutine(WaitAndReenablePlayer(3f));
                        var invDir = rb.velocity.normalized;
                        invDir.y = Mathf.Abs(new Vector2(invDir.x, invDir.z).magnitude) / 3f;
                        _rb.AddForce(invDir * _info.PropulsionForce * rb.velocity.magnitude, ForceMode.Impulse);
                        _rb.AddTorque(Random.insideUnitSphere.normalized * 10f);
                    }
                    var dir = collision.collider.transform.position - transform.position;
                    dir.y = Mathf.Abs(new Vector2(dir.x, dir.z).magnitude);
                    rb.AddForce(dir * _info.PropulsionForce, ForceMode.Impulse);
                }
            }
        }

        private IEnumerator WaitAndReenablePlayer(float time)
        {
            _isDisabled = true;
            yield return new WaitForSeconds(time);
            _isDisabled = false;
            _rb.isKinematic = true;
            _agent.enabled = true;
            UpdateDestination();
        }

        public void TriggerCollisionEffects(string tagName)
        {
            Debug.Log($"collision with {tagName}");
            switch (tagName)
            {
                case "Food":
                    FadeNumberAbovePlayer(1);
                    break;
            }
        }

        // handles negative amounts
        private void FadeNumberAbovePlayer(int n)
        {
            Vector3 pos = Camera.main.WorldToScreenPoint(transform.position);
           
            // if (n < 0)
            // {}
    
            GameObject fadingText = Instantiate(_fadingTextAnimationPrefab, _overlayCanvas.transform);
            fadingText.transform.position = pos;
            testImg.transform.position = pos;

            Destroy(fadingText, 1f);
        }
    }
}
