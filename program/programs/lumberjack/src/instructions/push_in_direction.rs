//! Instruction: Push in direction
use crate::PlayerData;
use anchor_lang::prelude::*;

use session_keys::{SessionError, SessionToken, session_auth_or, Session};
pub use crate::errors::Solana2048Error;

#[session_auth_or(
    ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
    Solana2048Error::WrongAuthority
)]
pub fn push_in_direction(mut ctx: Context<PushInDirection>, direction: u8, _counter: u8) -> Result<()> {
    let account = &mut ctx.accounts;
    let result = account.player.board.push(direction);
    account.player.score += result.0;

    save_highscore(&mut account.highscore, &mut account.player, &mut account.avatar.key());            

    //account.player.moved = result.1; // Probably not needed
    account.player.game_over = result.2;
    account.player.direction = direction;
    account.player.new_tile_x = result.3;
    account.player.new_tile_y = result.4;
    account.player.new_tile_level = result.5;
    msg!("Yo move tile moved:{} gamover:{} x:{} y:{} level:{}", result.1, result.2, result.3, result.4, result.5);
    Ok(())
}   

pub fn save_highscore(highscore: &mut Highscore, player: &mut PlayerData, avatar: &mut Pubkey) {
    // Check if the player already exists in the highscore list
    let mut found = false;
    for n in 0..highscore.global.len() {
        if highscore.global[n].nft.key() == avatar.key() {
            if highscore.global[n].score < player.score {
                highscore.global[n].score = player.score;
            }
            found = true;
        }
    }

    // If the player doesn't exist in the highscore list, add a new entry
    if !found {
        highscore.global.push(HighscoreEntry {
            score: player.score,
            player: player.authority.key(),
            nft: avatar.key(),
        });
        msg!("New highscore entry added");
        // Sort the highscore list in descending order and keep only the top 10 entries
        highscore.global.sort_unstable_by(|a, b| b.score.cmp(&a.score));
        highscore.global.truncate(10);
    }

    // Check if the player already exists in the highscore list
    let mut found = false;
    for n in 0..highscore.weekly.len() {
        if highscore.weekly[n].nft.key() == avatar.key() {
            if highscore.weekly[n].score < player.score {
                highscore.weekly[n].score = player.score;
            }
            found = true;
        }
    }

    // If the player doesn't exist in the highscore list, add a new entry
    if !found {
        highscore.weekly.push(HighscoreEntry {
            score: player.score,
            player: player.authority.key(),
            nft: avatar.key(),
        });
        msg!("New highscore entry added weekly");
        // Sort the highscore list in descending order and keep only the top 10 entries
        highscore.weekly.sort_unstable_by(|a, b| b.score.cmp(&a.score));
        highscore.weekly.truncate(10);
    }
}

#[derive(Accounts, Session)]
pub struct PushInDirection <'info> {
    #[session(
        // The ephemeral key pair signing the transaction
        signer = signer,
        // The authority of the user account which must have created the session
        authority = player.authority.key()
    )]
    // Session Tokens are passed as optional accounts
    pub session_token: Option<Account<'info, SessionToken>>,

    #[account( 
        mut,
        seeds = [b"player7".as_ref(), player.authority.key().as_ref(), avatar.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account( 
        mut,
        seeds = [b"highscore_list_v2".as_ref()],
        bump,
    )]
    pub highscore: Account<'info, Highscore>,
    #[account(mut)]
    pub signer: Signer<'info>,
    /// CHECK: Ppl can send in any avatar they want. Could check for mint 
    pub avatar: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

#[account]
pub struct Highscore {
    pub global: Vec<HighscoreEntry>,
    pub weekly: Vec<HighscoreEntry>,
}

#[derive(Default, AnchorSerialize, AnchorDeserialize, Clone, Copy, Debug)]
pub struct HighscoreEntry {
    pub score: u32,
    pub player: Pubkey,
    pub nft: Pubkey,
}