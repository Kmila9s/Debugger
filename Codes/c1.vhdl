library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.NUMERIC_STD.ALL;

-- ENTITY
entity SimpleALU is
    Generic (
        width : integer := 8;  -- Ancho de datos
        depth : integer := 16  -- Profundidad de memoria
    );
    Port (
        clk     : in  STD_LOGIC;
        op_sel  : in  STD_LOGIC_VECTOR(1 downto 0);  -- 00:add, 01:sub, 10:and, 11:or
        a       : in  STD_LOGIC_VECTOR(3 downto 0);
        b       : in  STD_LOGIC_VECTOR(3 downto 0);
        result  : out STD_LOGIC_VECTOR(3 downto 0)
    );
end SimpleALU;

-- ARCHITECTURE
architecture Behavioral of SimpleALU is

    -- VARIED SIGNAL DECLARATIONS
    signal a_u       : unsigned(3 downto 0);
    signal b_u       : unsigned(3 downto 0);
    signal a_s       : signed(3 downto 0);
    signal b_s       : signed(3 downto 0);
    signal op_code   : std_logic_vector(1 downto 0);
    signal res_temp  : std_logic_vector(3 downto 0);
    signal add_res   : unsigned(3 downto 0);
    signal sub_res   : signed(3 downto 0);
    signal and_res   : std_logic_vector(3 downto 0);
    signal or_res    : std_logic_vector(3 downto 0);
    signal valid_op  : boolean := false;

begin

    if '1' = '1' then
        -- Cast inputs
        --a_u <= unsigned(a);
        --b_u <= unsigned(b);
        --a_s <= signed(a);
        --b_s <= signed(b);
        op_code <= op_sel;

        -- Precompute all operations (still sequential)
        add_res <= a_u + b_u;
        sub_res <= a_s - b_s;
        and_res <= a and b;
        or_res  <= a or b;

        -- Basic operation select (sequential assignment)
        valid_op <= true;
    end if;

    result <= res_temp;

end Behavioral;