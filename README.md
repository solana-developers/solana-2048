This game was started from the games template lumberjack: https://github.com/solana-developers/solana-game-starter-kits/tree/main/lumberjack

You can try out a deployed demo here: https://solplay.de/solana-2048/

And download an apk here: https://solplay.de/solana-2048/solana2048.apk

Notice that to play you will need to create an account and it will automatically fund a session wallet. The sol in there you will get back when the session expires. 

# Disclaimer
Neither gum session token nor the solana-2048 program are audited. Use at your own risk.
This is an example game and not a finished product. It is not optimized for security.

Todos: 
- Check if there will be conjestion on the highscore account since its part of every move. Could be moved to a submit highscore function, but would be less nice. 
- Track performance of processed socket commitment using whirligig and without
- Fix Unity SDK nft loading, which throws exceptions when json doesnt exist and has a huge memory footprint
- Handle waiting for session token properly as soon as its possible to figure out if a transaction was rejected in UnitySDK
- Decide if it would make sense to verify that the NFT used for the PDA is actually owned by the player.


How to build this example:

Anchor program
1. Install the [Anchor CLI](https://project-serum.github.io/anchor/getting-started/installation.html)
2. `cd solana-2048` `cd program` to end the program directory
3. Run `anchor build` to build the program
4. Run `anchor deploy` to deploy the program
5. Copy the program id into the lib.rs and anchor.toml file
6. Build and deploy again

Unity client
1. Install Unity (https://unity.com)
2. Run the Scene Solana-2048
3. While in editor press the login editor button on the bottom left
4. Please adjust your RPC node URL in the Solana2048 Screen monobehaviour in the Solana2048 scene. (Helius, quicknode, triton and others all work for this. The performance differences are not significant according to my tests it is way more dependant on the validators) 

To generate a new version of the c# client use:
generate c# client: 
https://solanacookbook.com/gaming/porting-anchor-to-unity.html#generating-the-client
dotnet tool install Solana.Unity.Anchor.Tool
dotnet anchorgen -i target/idl/solana_twentyfourtyeight.json -o target/idl/ProgramCode.cs

# Solana-2048  

2048 is a simple game which is played on a 4x4 grid. The player can move the tiles in four directions.
If two tiles with the same number touch, they merge into one tile with the sum of the two tiles.
The goal of the game is to create a tile with the number 2048 or above.

## Everything is on chain 
In the Solana version of it every transaction is an on chain transaction and it is using an auto approve system called
gum session keys (https://github.com/gumhq/gpl/tree/master/programs/gpl_session), so signing every transaction is not needed but instead certain checked instructions in the program can be auto approved via the session token.
The speed of transaction approval is reached by using a websocket connecting with the commitment "processed" and rolling back the game state if the transaction is not confirmed within a certain time. It also supports the whirligig sockets when connecting to a Triton RPC node.

## Game state is saved on any NFT 
Furthermore the game state is bound to an NFT mint if the player selects an NFT. 
So the game state can actually be send to another player by sending the NFT to him and every game state can be loaded by just knowing the mint of the NFT.
From the rankings tab you can load any of the games of other players and check their current game state. 

## Weekly high score
With every new game a tiny amount of lamports is send to the programs jackpot treasury. This is then payed out to the player with the highest score at the end of each week by a clockwork (https://www.clockwork.xyz/) thread automatically.

## Client
The client is written in the game engine Unity and is using the Solana Unity SDK (https://github.com/magicblock-labs/Solana.Unity-SDK) to interact with the Solana blockchain.

## Program 
The program is written Anchor and rust. Anchor is a framework for Solana which makes it easier to write programs for Solana.
