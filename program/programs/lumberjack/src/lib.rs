pub mod state;
use anchor_lang::solana_program::pubkey;
pub use state::*;

use anchor_lang::prelude::*;
use gpl_session::{SessionError, SessionToken, session_auth_or, Session};
use anchor_lang::solana_program::native_token::LAMPORTS_PER_SOL;
use anchor_lang::system_program;

// Enable once clock work dependency is added
//use instructions::*;
//pub mod instructions;

declare_id!("6oKZFmFvcb69ThDuZjrsHABn4A6GMUpPGWNhxJKazWVB");
const ADMIN_PUBKEY: Pubkey = pubkey!("GsfNSuZFrT2r4xzSndnCSs9tTXwt47etPqU8yFVnDcXd");

#[error_code]
pub enum GameErrorCode {
    #[msg("Wrong Authority")]
    WrongAuthority,
    #[msg("Game not over yet")]
    GameNotOverYet,
}
/// Seed for thread_authority PDA.
pub const THREAD_AUTHORITY_SEED: &[u8] = b"authority";
const GAME_ENTRY: u64 = LAMPORTS_PER_SOL / 100; // 0.01 SOL

#[program]
pub mod lumberjack {

    use super::*;

    pub fn init_player(mut ctx: Context<InitPlayer>) -> Result<()> {
        let account = &mut ctx.accounts;
        msg!("init");
        account.player.board = account.player.board.Init();
        ctx.accounts.player.authority = ctx.accounts.signer.key();

        let cpi_context = CpiContext::new(
            ctx.accounts.system_program.to_account_info(),
            system_program::Transfer {
                from: ctx.accounts.signer.to_account_info().clone(),
                to: ctx.accounts.price_pool.to_account_info().clone(),
            },
        );
        system_program::transfer(cpi_context, GAME_ENTRY)?;
        Ok(())
    }

    /*
    Do the reward distribution via a clockwork thread as soon as dependencies are resolved.
    pub fn start_thread(ctx: Context<StartThread>, thread_id: Vec<u8>) -> Result<()> {
        instructions::start_thread(ctx, thread_id)
    }

    pub fn pause_thread(ctx: Context<PauseThread>, thread_id: Vec<u8>) -> Result<()> {
        instructions::pause_thread(ctx, thread_id)
    }

    pub fn resume_thread(ctx: Context<ResumeThread>, thread_id: Vec<u8>) -> Result<()> {
        instructions::resume_thread(ctx, thread_id)
    }*/

    pub fn reset_weekly_highscore(mut ctx: Context<ResetWeeklyHighscore>) -> Result<()> {

        msg!("Place 1: {}", ctx.accounts.place_1.to_account_info().key());
        let lamports = **ctx
                .accounts
                .price_pool
                .to_account_info()
                .try_borrow_mut_lamports()?;

        if lamports > 100000 {
            let available_for_distribution = lamports-100000;

            **ctx
                .accounts
                .price_pool
                .to_account_info()
                .try_borrow_mut_lamports()? -= available_for_distribution;
            **ctx
                .accounts
                .place_1
                .to_account_info()
                .try_borrow_mut_lamports()? += available_for_distribution;            
        }

        ctx.accounts.highscore.weekly = vec![];
        Ok(())
    }

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn push_in_direction(mut ctx: Context<PushInDirection>, direction: u8, counter: u8) -> Result<()> {
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

    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn restart(mut ctx: Context<PushInDirection>) -> Result<()> {
        let account = &mut ctx.accounts;
        // For testing allow always resetting the game
        //if (account.player.game_over) {
        account.player.board = account.player.board.Init();
        account.player.score = 0;
        account.player.game_over = false;
        account.player.new_tile_level = 0;
        //} else {
        //    return err!(GameErrorCode::NotEnoughEnergy);
        //}
        let cpi_context = CpiContext::new(
            ctx.accounts.system_program.to_account_info(),
            system_program::Transfer {
                from: ctx.accounts.signer.to_account_info().clone(),
                to: ctx.accounts.price_pool.to_account_info().clone(),
            },
        );
        system_program::transfer(cpi_context, GAME_ENTRY)?;

        msg!("Game reset");
        Ok(())
    }

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

#[derive(Accounts)]
pub struct InitPlayer <'info> {
    #[account( 
        init,
        payer = signer,
        space = 1000,
        seeds = [b"player7".as_ref(), signer.key().as_ref(), avatar.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,
    #[account( 
        init_if_needed,
        payer = signer,
        space = 10240,
        seeds = [b"highscore_list_v2".as_ref()],
        bump,
    )]
    pub highscore: Account<'info, Highscore>,
    #[account( 
        init_if_needed,
        payer = signer,
        space = 100,
        seeds = [b"price_pool".as_ref()],
        bump,
    )]
    pub price_pool: Account<'info, Pricepool>,
    #[account(mut)]
    pub signer: Signer<'info>,
    /// CHECK: Unchecked until I can get SPL and Meta data to work
    pub avatar: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

#[account]
pub struct PlayerData {
    pub authority: Pubkey,
    pub board: BoardData,
    pub score: u32,
    pub game_over: bool,
    pub direction: u8,
    pub top_tile: u32,
    pub new_tile_x: u8, 
    pub new_tile_y: u8,
    pub new_tile_level: u32,
}

#[account]
pub struct Highscore {
    pub global: Vec<HighscoreEntry>,
    pub weekly: Vec<HighscoreEntry>,
}

#[account]
pub struct Pricepool {
}

#[derive(Default, AnchorSerialize, AnchorDeserialize, Clone, Copy, Debug)]
pub struct HighscoreEntry {
    pub score: u32,
    pub player: Pubkey,
    pub nft: Pubkey,
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
    #[account( 
        seeds = [b"price_pool".as_ref()],
        bump,
    )]
    pub price_pool: Account<'info, Pricepool>,
    #[account(mut)]
    pub signer: Signer<'info>,
    /// CHECK: Unchecked until I can get SPL and Meta data to work
    pub avatar: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct ResetWeeklyHighscore <'info> {
    #[account( 
        mut,
        seeds = [b"highscore_list_v2".as_ref()],
        bump,
    )]
    pub highscore: Account<'info, Highscore>,
    /// CHECK: Can only be called by admin and will be replaced with a clock work thread later.
    pub place_1: AccountInfo<'info>,
    #[account( 
        seeds = [b"price_pool".as_ref()],
        bump,
    )]
    pub price_pool: Account<'info, Pricepool>,
    #[account(
        mut,
        address = ADMIN_PUBKEY
    )]
    pub signer: Signer<'info>,
    pub system_program: Program<'info, System>,
}