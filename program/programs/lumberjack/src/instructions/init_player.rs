//! Instruction: Push in direction
use crate::GAME_DEV_FEE;
use crate::state::*;
use anchor_lang::prelude::*;
use anchor_lang::system_program;
use crate::Highscore;
pub use crate::errors::Solana2048Error;
use crate::JACKPOT_ENTRY;

use super::Pricepool;

pub fn init_player(mut ctx: Context<InitPlayer>) -> Result<()> {
    let account = &mut ctx.accounts;
    msg!("init");
    account.player.board = account.player.board.init();
    ctx.accounts.player.authority = ctx.accounts.signer.key();

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
    Ok(())
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
    pub xp: u32,
    pub level: u32,
}

#[derive(Accounts)]
pub struct InitPlayer <'info> {
    #[account( 
        init,
        payer = signer,
        space = 800,
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
    /// CHECK: Unchecked, can be changed to the wallet of the person running the client to earn some money
    #[account(mut)]
    pub client_dev_wallet: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}