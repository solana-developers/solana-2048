//! Instruction: Push in direction
pub use crate::errors::Solana2048Error;
use crate::Highscore;
use anchor_lang::prelude::*;
use clockwork_sdk::state::Thread;
use crate::{THREAD_AUTHORITY_SEED};

pub fn reset_weekly_highscore(ctx: Context<ResetWeeklyHighscore>) -> Result<()> {

    let lamports = **ctx
        .accounts
        .price_pool
        .to_account_info()
        .try_borrow_mut_lamports()?;

    let mut amount_of_accounts = 0;
    if ctx.accounts.place_1.is_some() {
        amount_of_accounts += 1;
    }
    if ctx.accounts.place_2.is_some() {
        amount_of_accounts += 1;
    }
    if ctx.accounts.place_3.is_some() {
        amount_of_accounts += 1;
    }

    if lamports > 2000000 && amount_of_accounts > 0 {
        let available_for_distribution = (lamports - 2000000) / amount_of_accounts;

        match &ctx.accounts.place_1 {
            Some(place_1) => {
                **ctx
                .accounts
                .price_pool
                .to_account_info()
                .try_borrow_mut_lamports()? -= available_for_distribution;

                **place_1
                    .to_account_info()
                    .try_borrow_mut_lamports()? += available_for_distribution;
            }
            None => {}
        }
        match &ctx.accounts.place_2 {
            Some(place_2) => {
                **ctx
                .accounts
                .price_pool
                .to_account_info()
                .try_borrow_mut_lamports()? -= available_for_distribution;

                **place_2
                    .to_account_info()
                    .try_borrow_mut_lamports()? += available_for_distribution;
            }
            None => {}
        }
        match &ctx.accounts.place_3 {
            Some(place_3) => {
                **ctx
                .accounts
                .price_pool
                .to_account_info()
                .try_borrow_mut_lamports()? -= available_for_distribution;

                **place_3
                    .to_account_info()
                    .try_borrow_mut_lamports()? += available_for_distribution;
            }
            None => {}
        }
    }

    ctx.accounts.highscore.weekly = vec![];
    Ok(())
}


#[derive(Accounts)]
#[instruction(thread_id: Vec<u8>)]
pub struct ResetWeeklyHighscore <'info> {
    #[account( 
        mut,
        seeds = [b"highscore_list_v2".as_ref()],
        bump,
    )]
    pub highscore: Account<'info, Highscore>,
    /// CHECK: Can only be called from thread.
    #[account(mut)]
    pub place_1: Option<AccountInfo<'info>>,
    /// CHECK: Can only be called from thread.
    #[account(mut)]
    pub place_2: Option<AccountInfo<'info>>,
    /// CHECK: Can only be called from thread.
    #[account(mut)]
    pub place_3: Option<AccountInfo<'info>>,
    #[account( 
        mut,
        seeds = [b"price_pool".as_ref()],
        bump,
    )]
    pub price_pool: Account<'info, Pricepool>,
    pub system_program: Program<'info, System>,
    /// Address to assign to the newly created thread.
    #[account(signer, constraint = thread.authority.eq(&thread_authority.key()))]
    pub thread: Account<'info, Thread>,

    /// The pda that will own and manage the thread.
    #[account(seeds = [THREAD_AUTHORITY_SEED], bump)]
    pub thread_authority: SystemAccount<'info>,            
}

#[account]
pub struct Pricepool {
}