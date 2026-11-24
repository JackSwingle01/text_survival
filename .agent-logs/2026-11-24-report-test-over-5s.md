# Main's quick check is red: a report test over 5 seconds

Hypothesis: `ReportTests.Reports_change_nothing_in_the_world` fails CI's 5 s limit on main (5.05 to 5.12 s,
three runs, alone and in the suite) because of the five hours it steps; stepping fewer would fix it.

Revised: two hours took as long as five -> the time is the shared lived world (`SharedLived`: two days
stepped, built by the first test that asks). Its other two users were already LongRun, so in the quick
check this test alone paid for the build. It is tagged LongRun too: there the world is already built
(550 ms for this test), and the quick check no longer builds it at all.

Checked (Release): `ReportTests` quick half, six tests, the slowest 2 s; LongRun half, three tests pass.
