use anchor_lang::prelude::*;
use std::iter::ExactSizeIterator;
use std::ops::Index;
use std::ops::IndexMut;

#[derive(Default, AnchorSerialize, AnchorDeserialize, Clone, Copy, Debug)]
pub struct BoardData {
    pub data: [[u32; 4]; 4],
}

impl BoardData {
    pub fn new() -> Self {
        let data: [[u32; 4]; 4] = [[0; 4]; 4];
        let mut board_data = Self { data };
        board_data.insert();
        board_data.insert();
        board_data
    }

    pub fn init(&mut self) -> Self {
        let data: [[u32; 4]; 4] = [[0; 4]; 4];
        let mut board_data = Self { data };
        msg!("insert");
        board_data.insert();
        board_data.insert();
        board_data
    }

    pub fn reset(&mut self) {
        *self = Self::new();
    }

    fn insert(&mut self) -> (u8, u8, u32) {
        let slot = Clock::get().unwrap().slot;

        let mut rng = XorShift64 { a: slot };

        let mut r = (rng.next() % 4) as usize;
        let mut c = (rng.next() % 4) as usize;
        while self.data[r][c] != 0 {
            r = (rng.next() % 4) as usize;
            c = (rng.next() % 4) as usize;
        }
        msg!("insert at {} {}", r, c);
        let vals = vec![2, 4];
        let val = vals[(rng.next() % 2) as usize];
        msg!("val {}", val);

        self.data[r][c] = val;
        (r as u8, c as u8, val)
    }

    pub fn push(&mut self, lrud: u8) -> (u32, bool, bool, u8, u8, u32) {
        let (score, moved) = match lrud {
            0 => self.push_right(),
            1 => self.push_down(),
            2 => self.push_left(),
            3 => self.push_up(),
            _ => panic!("Invalid direction"),
        };
        if moved {
            let (x, y, number) = self.insert();
            let over = self.is_full() && !self.can_merge();
            (score, moved, over, x, y, number)
        } else {
            let over = self.is_full() && !self.can_merge();
            (score, moved, over, 0, 0, 0)
        }
    }

    fn push_left(&mut self) -> (u32, bool) {
        let score = self.merge_left();
        let moved = self._push_left();
        (score, score != 0 || moved)
    }

    fn push_right(&mut self) -> (u32, bool) {
        self.swap_lr();
        let result = self.push_left();
        self.swap_lr();
        result
    }

    fn push_up(&mut self) -> (u32, bool) {
        self.swap_diagnol();
        let result = self.push_left();
        self.swap_diagnol();
        result
    }

    fn push_down(&mut self) -> (u32, bool) {
        self.swap_ud();
        let result = self.push_up();
        self.swap_ud();
        result
    }

    fn merge_left(&mut self) -> u32 {
        let mut score = 0;
        for r in 0..4 {
            let mut i = 0;
            while i < 3 {
                if self.data[r][i] == 0 {
                    i += 1;
                    continue;
                }
                let mut j = i + 1;
                while j < 4 && self.data[r][j] == 0 {
                    j += 1;
                }
                if j == 4 {
                    break;
                }
                if self.data[r][i] == self.data[r][j] {
                    self.data[r][i] *= 2;
                    score += self.data[r][i];
                    self.data[r][j] = 0;
                    i = j + 1;
                } else {
                    i = j;
                }
            }
        }
        score
    }

    fn _push_left(&mut self) -> bool {
        let mut moved = false;
        for r in 0..4 {
            let mut i = 0;
            while i < 3 {
                if self.data[r][i] != 0 {
                    i += 1;
                    continue;
                }
                let mut j = i + 1;
                while j < 4 && self.data[r][j] == 0 {
                    j += 1;
                }
                if j == 4 {
                    break;
                }
                moved = true;
                self.data[r][i] = self.data[r][j];
                self.data[r][j] = 0;
                i += 1;
            }
        }
        moved
    }

    fn swap_lr(&mut self) {
        for r in 0..4 {
            for c in 0..2 {
                self.data[r].swap(c, 3 - c);
            }
        }
    }

    fn swap_diagnol(&mut self) {
        for r in 0..4 {
            for c in (r + 1)..4 {
                let tmp = self.data[r][c];
                self.data[r][c] = self.data[c][r];
                self.data[c][r] = tmp;
            }
        }
    }

    fn swap_ud(&mut self) {
        for r in 0..2 {
            for c in 0..4 {
                let tmp = self.data[r][c];
                self.data[r][c] = self.data[3 - r][c];
                self.data[3 - r][c] = tmp;
            }
        }
    }

    fn is_full(&self) -> bool {
        for i in 0..4 {
            for j in 0..4 {
                if self.data[i][j] == 0 {
                    return false;
                }
            }
        }
        true
    }

    fn can_merge(&self) -> bool {
        for i in 0..4 {
            for j in 0..4 {
                if i != 3 && self.data[i][j] == self.data[i + 1][j] {
                    return true;
                }
                if j != 3 && self.data[i][j] == self.data[i][j + 1] {
                    return true;
                }
            }
        }
        false
    }
}

impl Index<usize> for BoardData {
    type Output = [u32; 4];

    fn index(&self, index: usize) -> &Self::Output {
        &self.data[index]
    }
}

impl IndexMut<usize> for BoardData {
    fn index_mut(&mut self, index: usize) -> &mut Self::Output {
        &mut self.data[index]
    }
}

impl Iterator for BoardData {
    type Item = [u32; 4];

    fn next(&mut self) -> Option<Self::Item> {
        self.data.iter_mut().next().copied()
    }
}

impl ExactSizeIterator for BoardData {
    fn len(&self) -> usize {
        self.data.len()
    }
}

pub struct XorShift64 {
    a: u64,
}

impl XorShift64 {
    pub fn next(&mut self) -> u64 {
        let mut x = self.a;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        self.a = x;
        x
    }
}
