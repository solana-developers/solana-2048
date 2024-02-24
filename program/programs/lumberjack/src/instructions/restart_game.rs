//! Instruction: Push in direction
use crate::GAME_DEV_FEE;
use crate::JACKPOT_ENTRY;
pub use crate::errors::Solana2048Error;
use crate::Highscore;
use anchor_lang::prelude::*;
use anchor_lang::system_program;
use session_keys::{SessionError, SessionToken, session_auth_or, Session};

use super::Pricepool;
use super::init_player::PlayerData;

#[session_auth_or(
    ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
    Solana2048Error::WrongAuthority
)]
pub fn restart(mut ctx: Context<Restart>) -> Result<()> {
    let account = &mut ctx.accounts;
    // For testing allow always resetting the game
    //if (account.player.game_over) {
    account.player.board = account.player.board.init();
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
    system_program::transfer(cpi_context, JACKPOT_ENTRY)?;

    let cpi_context = CpiContext::new(
        ctx.accounts.system_program.to_account_info(),
        system_program::Transfer {
            from: ctx.accounts.signer.to_account_info().clone(),
            to: ctx.accounts.client_dev_wallet.to_account_info().clone(),
        },
    );
    system_program::transfer(cpi_context, GAME_DEV_FEE)?;

    msg!("Game reset");
    Ok(())
}

#[derive(Accounts, Session)]
pub struct Restart <'info> {
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
        mut,
        seeds = [b"price_pool".as_ref()],
        bump,
    )]
    pub price_pool: Account<'info, Pricepool>,
    #[account(mut)]
    pub signer: Signer<'info>,
    /// CHECK: Unchecked until I can get SPL and Meta data to work
    pub avatar: UncheckedAccount<'info>,
    /// CHECK: Unchecked, can be changed to the wallet of the person running the client to earn some money
    #[account(mut)]
    pub client_dev_wallet: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}
