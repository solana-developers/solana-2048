//! Instruction: pause_thread
use crate::THREAD_AUTHORITY_SEED;
use anchor_lang::prelude::*;
use clockwork_sdk::state::Thread;

use clockwork_sdk::state::ThreadAccount;

pub fn reset_thread(ctx: Context<ResetThread>, _thread_id: Vec<u8>) -> Result<()> {
    // Get accounts
    let clockwork_program = &ctx.accounts.clockwork_program;
    let payer = &ctx.accounts.payer;
    let thread = &ctx.accounts.thread;
    let thread_authority = &ctx.accounts.thread_authority;

    // Delete thread via CPI.
    let bump = *ctx.bumps.get("thread_authority").unwrap();
    clockwork_sdk::cpi::thread_delete(CpiContext::new_with_signer(
        clockwork_program.to_account_info(),
        clockwork_sdk::cpi::ThreadDelete {
            authority: thread_authority.to_account_info(),
            close_to: payer.to_account_info(),
            thread: thread.to_account_info(),
        },
        &[&[THREAD_AUTHORITY_SEED, &[bump]]],
    ))?;
    Ok(())
}

#[derive(Accounts)]
pub struct ResetThread<'info> {
    #[account(mut)]
    pub payer: Signer<'info>,

    /// The Clockwork thread program.
    #[account(address = clockwork_sdk::ID)]
    pub clockwork_program: Program<'info, clockwork_sdk::ThreadProgram>,

    /// The thread to reset.
    #[account(mut, address = thread.pubkey(), constraint = thread.authority.eq(&thread_authority.key()))]
    pub thread: Account<'info, Thread>,

    /// The pda that owns and manages the thread.
    #[account(seeds = [THREAD_AUTHORITY_SEED], bump)]
    pub thread_authority: SystemAccount<'info>,
}
