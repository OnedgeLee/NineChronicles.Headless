using Bencodex.Types;
using Libplanet.Extensions.PluginActionEvaluator;
using Libplanet.RocksDBStore;
using Libplanet.Store;

string pluginPath = "C:\\Users\\onedg\\planet\\fork\\Plug\\Lib9c.PluginActionEvaluator.dll";
string storePath = "C:\\Users\\onedg\\planet\\fork\\Plug\\store";

var store = new TrieStateStore(new RocksDBKeyValueStore(storePath));

var trie = store.GetStateRoot(null);

var trie2 = trie.Set(new Libplanet.Store.Trie.KeyBytes("asdf"), (Text)"zxcv");
var trie3 = store.Commit(trie2);


var plugged = new PluggedActionEvaluator(pluginPath, "Lib9c.PluginActionEvaluator.PluginActionEvaluator", storePath);


Console.WriteLine(plugged.HasTrie(trie3.Hash.ToByteArray()));
