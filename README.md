How to run this example:

Many things are still called lumberjack because this project is based on the lumberjack example from the game starter kits.

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
gum session keys, so signing every transaction is not needed.

## Game state is saved on any NFT 
Furthermore the game state is bound to an NFT ming if the player selects an NFT. 
So the game state can actually be send to another player by sending the NFT to him.

## Weekly highscore
With every new game a tiny amount of lamports is send to the program. This is then payed out to the player with the highest score at the end of the week. (This is supposed to happen with a clockwork thread, but currently its manual because I cant figure out how to make it work together with session keys. If you can figure it out please let me know.)

## Client
The client is written in the game engine Unity and is using the Solana Unity SDK to interact with the Solana blockchain.

## Program 
The program is written Anchor and rust. Anchor is a framework for Solana which makes it easier to write programs for Solana.
